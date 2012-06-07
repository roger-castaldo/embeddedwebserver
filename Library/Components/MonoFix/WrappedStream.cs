using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Net.Sockets;
using System.Threading;

namespace Org.Reddragonit.EmbeddedWebServer.Components.MonoFix
{
    internal class WrappedStream : IDisposable
    {
        private const int _READ_TIMEOUT = 5000;
        private bool _override;
        private Stream _stream;
        private ManualResetEvent _waitHandle;
        private byte[] _buffer;
        private int _offset;
        private int _count;
        private AsyncCallback _callBack;
        private WrappedStreamAsyncResult _result;
        private bool _closed;
        private Thread _thread;
        private ManualResetEvent _resBeginRead;
        private int _readCount;
        private bool _reading;

        public WrappedStream(SslStream stream)
        {
            _stream = stream;
            _Init();
        }

        public WrappedStream(NetworkStream stream)
        {
            _stream = stream;
            _Init();
        }

        private void _Init()
        {
            if (Utility.MonoVersion == null)
                _override = false;
            else
                _override = Utility.MonoVersion < Utility._OVERRIDE_VERSION;
            if (_override)
            {
                if (_stream.ReadTimeout > _READ_TIMEOUT)
                    _stream.ReadTimeout = _READ_TIMEOUT;
                _closed = false;
                _waitHandle = new ManualResetEvent(false);
                _resBeginRead = new ManualResetEvent(false);
                _thread = new Thread(new ThreadStart(_BackgroundRead));
                _thread.IsBackground = true;
            }
        }

        internal void AuthenticateAsServer(X509Certificate certificate)
        {
            ((SslStream)_stream).AuthenticateAsServer(certificate);
        }

        private void _BackgroundRead()
        {
            while (!_closed)
            {
                _resBeginRead.WaitOne();
                _resBeginRead.Reset();
                if (!_closed)
                {
                    try
                    {
                        _reading = true;
                        _readCount = _stream.Read(_buffer, _offset, _count);
                        _reading = false;
                    }
                    catch (Exception e)
                    {
                        _readCount = 0;
                    }
                    if (!_closed)
                    {
                        _result.Complete();
                        _waitHandle.Set();
                        try
                        {
                            if (_callBack != null)
                                _callBack.Invoke(_result);
                        }
                        catch (Exception e) { }
                    }
                }
            }
        }

        internal IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callBack,object state)
        {
            if (!_override)
                return _stream.BeginRead(buffer, offset, count, callBack, state);
            else
            {
                _buffer = buffer;
                _offset = offset;
                _count = count;
                _callBack = callBack;
                _result = new WrappedStreamAsyncResult(state, _waitHandle);
                if (_thread.ThreadState == ThreadState.Unstarted)
                    _thread.Start();
                _resBeginRead.Set();
                return _result;
            }
        }

        internal int EndRead(IAsyncResult res)
        {
            if (!_override)
                return _stream.EndRead(res);
            else
                return _readCount;
        }

        internal void Write(byte[] buffer, int offset, int count)
        {
            _stream.Write(buffer, offset, count);
        }

        internal void Flush()
        {
            _stream.Flush();
        }

        #region IDisposable Members

        public void Dispose()
        {
            if (_override)
            {
                _closed=true;
                if (!_reading)
                    _resBeginRead.Set();
                else
                {
                    try
                    {
                        _thread.Abort();
                    }
                    catch (Exception e) { }
                }
            }
            _stream.Dispose();
        }

        #endregion
    }
}
