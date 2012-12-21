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
using Org.Reddragonit.EmbeddedWebServer.Components.Readers;
using Org.Reddragonit.EmbeddedWebServer.Components.MonoFix;

namespace Org.Reddragonit.EmbeddedWebServer.Components
{
    /*
     * This class is a wrapper for the http connection established by
     * the client.  It houses the underlying socket as well as 
     * the cookie and header information.
     */
    internal class HttpConnection
    {
        private const int _CONNECTION_IDLE_TIMEOUT = 60000; //keep alive for 100 seconds
        private const int _BUFFER_SIZE = 65535;

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

        private static readonly ObjectPool<HttpRequest> Requests = new ObjectPool<HttpRequest>(() => new HttpRequest());

        //the maximium size of a post data allowed
        private static int MAX_POST_SIZE = 10 * 1024 * 1024; // 10MB

        //the underlying socket for the connection
        private Socket socket;
        private X509Certificate _cert;
        private List<HttpRequest> _requests;
        private WrappedStream _inputStream;
        private bool _shutdown;
        private bool _disposed;
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
            if (socket != null)
            {
                if (_idleTimer != null)
                {
                    _idleTimer.Dispose();
                    _idleTimer = null;
                }
                try
                {
                    socket.Disconnect(false);
                    socket.Close();
                    socket = null;
                }
                catch (Exception e) { }
                try
                {
                    _inputStream.Dispose();
                    _inputStream = null;
                }
                catch (Exception e)
                {
                    Logger.Error(e.Message);
                }
            }
            _buffer = null;
            _parser.RequestLineRecieved = null;
            _parser.Clear();
            if (!_disposed)
                _listener.ClearConnection(this);
        }

        /*
         * This constructor loads and http connection from a given tcp client.
         * It establishes the required streams and objects, then loads in the 
         * header information, it avoids loading in post data for efficeincy.
         * The post data gets loaded later on when the parameters are accessed.
         */
        internal HttpConnection(){
            _disposed = false;
            _parser = new HttpParser();
        }
        
        internal void Start(Socket s, PortListener listener, X509Certificate cert,long id)
        {
            _idleTimer = new Timer(new TimerCallback(_IdleTimeout), null, _CONNECTION_IDLE_TIMEOUT, Timeout.Infinite);
            _rand = new MT19937(id);
            _buffer = new byte[_BUFFER_SIZE];
            _requests = new List<HttpRequest>();
            _parser.RequestLineRecieved = _RequestLineRecieved;
            _shutdown = false;
            _id = id;
            socket = s;
            _listener = listener;
            _cert = cert;
            _currentConnection = this;
            if (_listener.UseSSL)
            {
                _inputStream = new WrappedStream(new SslStream(new NetworkStream(socket), true));
                _inputStream.AuthenticateAsServer(_cert);
            }
            else
                _inputStream = new WrappedStream(new NetworkStream(socket));
            _inputStream.BeginRead(_buffer, 0, _buffer.Length, new AsyncCallback(OnReceive), null);
        }

        private void _IdleTimeout(object state)
        {
            _currentConnection = this;
            SendBuffer(Encoding.Default.GetBytes("HTTP/1.0 " + ((int)HttpStatusCode.RequestTimeout).ToString() + " Failed to send a request before the idle timeout"), false);
            if (!_shutdown)
                Close();
        }

        internal void ResetTimer()
        {
            if (_idleTimer != null)
                _idleTimer.Change(_CONNECTION_IDLE_TIMEOUT, Timeout.Infinite);
        }

        private void OnReceive(IAsyncResult ar)
        {
            _currentConnection = this;
            if (_idleTimer != null)
            {
                try
                {
                    _idleTimer.Change(_CONNECTION_IDLE_TIMEOUT, Timeout.Infinite);
                }
                catch (Exception e) { }
            }
            // been closed by our side.
            if (_inputStream == null)
                return;
            try
            {
                int bytesLeft = _inputStream.EndRead(ar);
                if (bytesLeft == 0)
                {
                    Logger.Trace("Client disconnected.");
                    if (_requests.Count > 0)
                    {
                        throw new ParserException("Failed to send complete request");
                    }
                    else
                    {
                        Close();
                        return;
                    }
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
                try
                {
                    _inputStream.BeginRead(_buffer, 0, _buffer.Length - offset, OnReceive, null);
                }
                catch (Exception e)
                {
                    Close();
                }
            }
            catch (ParserException err)
            {
                Logger.Trace(err.ToString());
                SendBuffer(Encoding.Default.GetBytes("HTTP/1.0 " + ((int)HttpStatusCode.BadRequest).ToString() + " " + err.Message),false);
                Close();
            }
            catch (Exception err)
            {
                if (!(err is IOException))
                {
                    Logger.Error("Failed to read from stream: " + err);
                    SendBuffer(Encoding.Default.GetBytes("HTTP/1.0 " + ((int)HttpStatusCode.InternalServerError).ToString() + " " + err.Message),false);
                }
                Close();
            }
        }

        internal void SendBuffer(byte[] buffer,bool shutdown)
        {
            HttpConnection.SetCurrentConnection(this);
            if (socket == null)
            {
                Close();
                return;
            }
            if (socket.Connected)
            {
                lock (_inputStream)
                {
                    try
                    {
                        Logger.Trace("Sending chunk of data size " + buffer.Length.ToString());
                        _inputStream.Write(buffer, 0, buffer.Length);
                        _inputStream.Flush();
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e.Message);
                    }
                }
            }
            if (shutdown)
            {
                Close();
            }
            else
                _idleTimer = new Timer(new TimerCallback(_IdleTimeout), null, _CONNECTION_IDLE_TIMEOUT, Timeout.Infinite);

        }

        private void _RequestLineRecieved(string[] words)
        {
            _idleTimer.Dispose();
            _idleTimer = new Timer(new TimerCallback(_IdleTimeout), null, _CONNECTION_IDLE_TIMEOUT, Timeout.Infinite);
            _currentConnection = this;
            if (words[0].ToUpper() != "HTTP"&&!_shutdown)
            {
                HttpRequest req = Requests.Dequeue();
                lock (_requests)
                {
                    req.StartRequest(_rand.NextLong(), words, this, ref _parser);
                    _requests.Add(req);
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
            _disposed = true;
            if (!_shutdown)
                Close();
            lock (_requests)
            {
                while (_requests.Count > 0)
                {
                    try
                    {
                        _requests[0].Reset();
                        Requests.Enqueue(_requests[0]);
                        _requests.RemoveAt(0);
                    }
                    catch (Exception e) { }
                }
            }
            try
            {
                _idleTimer.Dispose();
                _idleTimer = null;
            }
            catch (Exception e) { }
        }

        #endregion

        internal void HeaderComplete()
        {
            _idleTimer.Dispose();
            _idleTimer = null;
        }

        internal void CompleteRequest(HttpRequest httpRequest)
        {
            if (!_shutdown)
            {
                lock (_requests)
                {
                    for (int x = 0; x < _requests.Count; x++)
                    {
                        if (_requests[x].ID == httpRequest.ID)
                            _requests.RemoveAt(x);
                    }
                }
                httpRequest.Reset();
                Requests.Enqueue(httpRequest);
                _idleTimer = new Timer(new TimerCallback(_IdleTimeout), null, _CONNECTION_IDLE_TIMEOUT, Timeout.Infinite);
            }
            else
                DisposeRequest(httpRequest);
        }

        internal void Reset()
        {
            try
            {
                _idleTimer.Dispose();
                _idleTimer = null;
            }
            catch (Exception e) { }
            _parser.Reset();
            lock (_requests)
            {
                while (_requests.Count > 0)
                {
                    try
                    {
                        _requests[0].Reset();
                        Requests.Enqueue(_requests[0]);
                        _requests.RemoveAt(0);
                    }
                    catch (Exception e) { }
                }
            }
        }

        internal void ClearTimer()
        {
            if (_idleTimer != null)
            {
                _idleTimer.Dispose();
                _idleTimer = null;
            }
        }
    }
}
