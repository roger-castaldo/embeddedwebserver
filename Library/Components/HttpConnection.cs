using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.IO;
using System.Collections;
using System.Threading;
using System.Collections.Specialized;
using Org.Reddragonit.EmbeddedWebServer.Interfaces;
using Org.Reddragonit.EmbeddedWebServer.Sessions;
using System.Net;
using Procurios.Public;
using Org.Reddragonit.EmbeddedWebServer.Diagnostics;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Org.Reddragonit.EmbeddedWebServer.Components.Message;

namespace Org.Reddragonit.EmbeddedWebServer.Components
{
    /*
     * This class is a wrapper for the http connection established by
     * the client.  It houses the underlying socket as well as 
     * the cookie and header information.
     */
    internal class HttpConnection : IDisposable
    {
        private const int _CONNECTION_IDLE_TIMEOUT = 300000;

        //A thread specific instance of the current connection
        [ThreadStatic()]
        private static HttpConnection _currentConnection = null;
        internal static void SetCurrentConnection(HttpConnection con)
        {
            _currentConnection = con;
        }
        public static HttpConnection CurrentConnection
        {
            get { return _currentConnection; }
        }

        private static readonly ObjectPool<byte[]> Buffers = new ObjectPool<byte[]>(() => new byte[65535]);

        //the maximium size of a post data allowed
        private static int MAX_POST_SIZE = 10 * 1024 * 1024; // 10MB

        //the underlying socket for the connection
        private Socket socket;
        private X509Certificate _cert;
        private List<HttpRequest> _requests;
        private Stream _inputStream;
        private bool _shutdown;
        private HttpParser _parser;
        private byte[] _buffer;
        private MT19937 _rand;
        private Timer _idleTimer;
        private PortListener _listener;
        public PortListener Listener
        {
            get { return _listener; }
        }

        //returns the client endpoint information
        public EndPoint Client
        {
            get { return socket.RemoteEndPoint; }
        }

        //returns the local endpoint information
        public EndPoint LocalEndPoint
        {
            get { return socket.LocalEndPoint; }
        }

        private long _id;
        public long ID
        {
            get { return _id; }
        }

        public override bool Equals(object obj)
        {
            if (obj is HttpConnection)
                return ((HttpConnection)obj).ID == ID;
            return false;
        }

        public void Close()
        {
            _shutdown = true;
            try
            {
                socket.Close();
            }
            catch (Exception e) { }
            Buffers.Enqueue(_buffer);
        }

        /*
         * This constructor loads and http connection from a given tcp client.
         * It establishes the required streams and objects, then loads in the 
         * header information, it avoids loading in post data for efficeincy.
         * The post data gets loaded later on when the parameters are accessed.
         */
        internal HttpConnection(Socket s, PortListener listener, X509Certificate cert,long id)
        {
            _idleTimer = new Timer(new TimerCallback(_IdleTimeout), null, _CONNECTION_IDLE_TIMEOUT, Timeout.Infinite);
            _rand = new MT19937(id);
            _buffer = Buffers.Dequeue();
            _requests = new List<HttpRequest>();
            _parser = new HttpParser(_RequestLineRecieved);
            _shutdown = false;
            _id = id;
            socket = s;
            _listener = listener;
            _cert = cert;
            _currentConnection = this;
            if (_listener.UseSSL)
            {
                _inputStream = new SslStream(new NetworkStream(socket), true);
                ((SslStream)_inputStream).AuthenticateAsServer(_cert);
            }
            else
                _inputStream = new NetworkStream(socket);
            _inputStream.BeginRead(_buffer, 0, _buffer.Length, new AsyncCallback(OnReceive), null);
        }

        private void _IdleTimeout(object state)
        {
            _currentConnection = this;
            _listener.DisposeOfConnection(this);
        }

        private void OnReceive(IAsyncResult ar)
        {
            _currentConnection = this;
            // been closed by our side.
            if (_inputStream == null)
                return;
            try
            {
                int bytesLeft = _inputStream.EndRead(ar);
                if (bytesLeft == 0)
                {
                    Logger.Trace("Client disconnected.");
                    Close();
                    return;
                }

                Logger.Debug(Client.ToString() + " received " + bytesLeft + " bytes.");

                if (bytesLeft < 5000)
                {
                    string temp = Encoding.Default.GetString(_buffer, 0, bytesLeft);
                    Logger.Trace(temp);
                }

                int offset = _parser.Parse(_buffer,0,bytesLeft);
                bytesLeft -= offset;

                if (bytesLeft > 0)
                {
                    Logger.Trace("Moving " + bytesLeft + " from " + offset + " to beginning of array.");
                    Buffer.BlockCopy(_buffer, offset, _buffer, 0, bytesLeft);
                }
                _inputStream.BeginRead(_buffer, 0, _buffer.Length - offset, OnReceive, null);
            }
            catch (ParserException err)
            {
                Logger.Trace(err.ToString());
                SendBuffer(Encoding.Default.GetBytes("HTTP/1.0 " + ((int)HttpStatusCode.BadRequest).ToString() + " " + err.Message),true);
                _inputStream.Flush();
                Close();
            }
            catch (Exception err)
            {
                if (!(err is IOException))
                {
                    Logger.Error("Failed to read from stream: " + err);
                    SendBuffer(Encoding.Default.GetBytes("HTTP/1.0 " + ((int)HttpStatusCode.InternalServerError).ToString() + " " + err.Message),true);
                    _inputStream.Flush();
                }
                Close();
            }
        }

        internal void SendBuffer(byte[] buffer,bool shutdown)
        {
            lock (_inputStream)
            {
                int index = 0;
                while (buffer.Length - index > socket.SendBufferSize)
                {
                    Logger.Trace("Sending chunk of data size " + socket.SendBufferSize.ToString());
                    _inputStream.Write(buffer, index, socket.SendBufferSize);
                    index += socket.SendBufferSize;
                }
                if (index < buffer.Length)
                {
                    Logger.Trace("Sending chunkc of data size " + (buffer.Length - index).ToString());
                    _inputStream.Write(buffer, index, buffer.Length - index);
                }
                _inputStream.Flush();
            }
            if (shutdown)
            {
                if (_idleTimer!=null)
                    _idleTimer.Change(1000, Timeout.Infinite);
                else
                    _idleTimer = new Timer(new TimerCallback(_IdleTimeout), null, 1000, Timeout.Infinite);
            }
            else
                _idleTimer = new Timer(new TimerCallback(_IdleTimeout), null, _CONNECTION_IDLE_TIMEOUT, Timeout.Infinite);
        }

        private void _RequestLineRecieved(string[] words)
        {
            _idleTimer.Dispose();
            _currentConnection = this;
            if (words[0].ToUpper() != "HTTP"&&!_shutdown)
            {
                lock (_requests)
                {
                    _requests.Add(new HttpRequest(_rand.NextLong(),words, this, ref _parser));
                }
            }
        }

        internal void DisposeRequest(HttpRequest httpRequest)
        {
            lock (_requests)
            {
                for (int x = 0; x < _requests.Count; x++)
                {
                    if (_requests[x].ID == httpRequest.ID)
                    {
                        _requests.RemoveAt(x);
                    }
                }
            }
            httpRequest.Dispose();
        }

        #region IDisposable Members

        public void Dispose()
        {
            if (!_shutdown)
                Close();
            lock (_requests)
            {
                while (_requests.Count > 0)
                {
                    try
                    {
                        _requests[0].Dispose();
                        _requests.RemoveAt(0);
                    }
                    catch (Exception e) { }
                }
            }
            try
            {
                _idleTimer.Dispose();
            }
            catch (Exception e) { }
        }

        #endregion
    }
}
