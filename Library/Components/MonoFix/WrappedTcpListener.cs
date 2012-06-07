using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Threading;
using Org.Reddragonit.EmbeddedWebServer.Diagnostics;

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
        private AsyncCallback _callBack;
        private WrappedTcpListenerAsyncResult _result;
        private bool _accepting;

        public WrappedTcpListener(TcpListener listener)
        {
            _listener = listener;
            if (Utility.MonoVersion == null)
                _override = false;
            else
                _override = Utility.MonoVersion < Utility._OVERRIDE_VERSION;
            if (_override){
                Logger.Trace("Using the wrapped Tcp Listener since running on old version of mono.");
                _closed = false;
                _waitHandle = new ManualResetEvent(false);
                _resBeginAccept = new ManualResetEvent(false);
                _thread = new Thread(new ThreadStart(_BackgroundAccept));
                _thread.IsBackground = true;
            }
        }

        private void _BackgroundAccept()
        {
            Logger.Trace("Background Accept function called for WrappedTcpListener");
            while (!_closed)
            {
                _resBeginAccept.WaitOne();
                _resBeginAccept.Reset();
                if (!_closed)
                {
                    Socket _socket = null;
                    try
                    {
                        Logger.Trace("Trying to accept a new socket");
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
                        if (_socket != null)
                            Logger.Trace("Wrapped Listener accepted a socket, attempting to call the accept callback");
                        _result.Complete(_socket);
                        _waitHandle.Set();
                        try
                        {
                            if (_callBack != null)
                                ThreadPool.QueueUserWorkItem(new WaitCallback(_ProcCallBack), (object)(new object[] { _callBack,_result}));
                        }
                        catch (Exception e) { }
                    }
                }
            }
        }

        private void _ProcCallBack(object obj)
        {
            AsyncCallback callBack = (AsyncCallback)((object[])obj)[0];
            WrappedTcpListenerAsyncResult result = (WrappedTcpListenerAsyncResult)((object[])obj)[1];
            callBack.Invoke(result);
        }

        public void Start(int backlog)
        {
            _listener.Start(backlog);
        }

        public IAsyncResult BeginAcceptSocket(AsyncCallback callback,object state)
        {
            Logger.Trace("Begin Accept Socket called in WrappedTcpClient");
            if (!_override)
                return _listener.BeginAcceptSocket(callback, state);
            else
            {
                if (_result != null)
                    throw new Exception("Unable to begin Accepting Socket, already asynchronously waiting");
                Logger.Trace("Accepting Socket through WrappedTcpClient override");
                _callBack = callback;
                _result = new WrappedTcpListenerAsyncResult(state, _waitHandle);
                if ((int)(_thread.ThreadState & ThreadState.Unstarted) == (int)ThreadState.Unstarted)
                {
                    Logger.Trace("WrappedTcpClient background thread not started, calling start");
                    _thread.Start();
                }
                Logger.Trace("Setting BeginAccept event to trigger background thread in WrappedTcpClient");
                _resBeginAccept.Set();
                return _result;
            }
        }

        public Socket EndAcceptSocket(IAsyncResult asyncResult)
        {
            if (!_override)
                return _listener.EndAcceptSocket(asyncResult);
            else
            {
                _result = null;
                if (asyncResult == null)
                    throw new Exception("Unable to handle null async result.");
                return ((WrappedTcpListenerAsyncResult)asyncResult).Socket;
            }
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
