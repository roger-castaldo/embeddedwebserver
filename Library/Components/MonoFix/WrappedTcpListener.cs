using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Threading;

namespace Org.Reddragonit.EmbeddedWebServer.Components.MonoFix
{
    internal class WrappedTcpListener
    {
        private TcpListener _listener;
        private bool _override;
        private ManualResetEvent _waitHandle;
        private bool _closed=false;
        private Thread _thread;
        private ManualResetEvent _resBeginAccept;
        private Socket _socket;
        private AsyncCallback _callBack;
        private WrappedTcpListenerAsyncResult _result;
        private bool _accepting;

        public WrappedTcpListener(TcpListener listener)
        {
            _listener = listener;
            if (Utility.MonoVersion == null)
                _override = false;
            else
            {
                _override = Utility.MonoVersion < Utility._OVERRIDE_VERSION;
                _waitHandle = new ManualResetEvent(false);
                _resBeginAccept = new ManualResetEvent(false);
                _thread = new Thread(new ThreadStart(_BackgroundAccept));
                _thread.IsBackground = true;
            }
        }

        private void _BackgroundAccept()
        {
            while (!_closed)
            {
                _resBeginAccept.WaitOne();
                _resBeginAccept.Reset();
                if (!_closed)
                {
                    try
                    {
                        _accepting = true;
                        _socket = _listener.AcceptSocket();
                        _accepting = false;
                    }
                    catch (Exception e)
                    {
                        _socket = null;
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

        public void Start(int backlog)
        {
            _listener.Start(backlog);
        }

        public IAsyncResult BeginAcceptSocket(AsyncCallback callback,object state)
        {
            if (!_override)
                return _listener.BeginAcceptSocket(callback, state);
            else
            {
                _callBack = callback;
                _result = new WrappedTcpListenerAsyncResult(state, _waitHandle);
                if (_thread.ThreadState == ThreadState.Unstarted)
                    _thread.Start();
                _resBeginAccept.Set();
                return _result;
            }
        }

        public Socket EndAcceptSocket(IAsyncResult asyncResult)
        {
            if (!_override)
                return _listener.EndAcceptSocket(asyncResult);
            else
                return _socket;
        }

        public void Stop()
        {
            if (_override)
            {
                _closed = true;
                if (!_accepting)
                    _resBeginAccept.Set();
                else
                {
                    try
                    {
                        _thread.Abort();
                    }
                    catch (Exception e) { }
                }
            }
            _listener.Stop();
        }
    }
}
