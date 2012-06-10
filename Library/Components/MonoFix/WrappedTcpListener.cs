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
        private static readonly ObjectPool<WrappedTcpListenerAsyncResult> _Results = new ObjectPool<WrappedTcpListenerAsyncResult>(() => new WrappedTcpListenerAsyncResult());

        private TcpListener _listener;
        private bool _override;
        private ManualResetEvent _waitHandle;
        private Thread _thread=null;
        private bool _closed;

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
            }
        }

        private void _BackgroundAccept(object obj)
        {
            WrappedTcpListenerAsyncResult result = (WrappedTcpListenerAsyncResult)obj;
            Socket socket = null;
            try
            {
                Logger.Trace("Trying to accept a new socket");
                socket = _listener.AcceptSocket();
            }catch (ThreadAbortException tae){
                result.Reset();
                _Results.Enqueue(result);
                throw tae;
            }
            catch (Exception e)
            {
                socket = null;
            }
            if (!_closed)
            {
                if (socket != null)
                    Logger.Trace("Wrapped Listener accepted a socket, attempting to call the accept callback");
                result.Complete(socket);
                _waitHandle.Set();
                ThreadPool.QueueUserWorkItem(new WaitCallback(_ProcCallBack), result);
            }
        }

        private void _ProcCallBack(object obj)
        {
            if ((int)(_thread.ThreadState & ThreadState.Stopped) != (int)ThreadState.Stopped)
            {
                try
                {
                    _thread.Join();
                }
                catch (Exception e) { }
            }
            WrappedTcpListenerAsyncResult result = (WrappedTcpListenerAsyncResult)obj;
            try
            {
                if (result.CallBack != null)
                    result.CallBack(result);
            }
            catch (Exception e) { }
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
                if (_thread != null)
                {
                    if (((int)(_thread.ThreadState & ThreadState.Unstarted) != (int)ThreadState.Unstarted)
                        && ((int)(_thread.ThreadState & ThreadState.Stopped) != (int)ThreadState.Stopped))
                        throw new Exception("Unable to begin Accepting Socket, already asynchronously waiting");
                }
                Logger.Trace("Accepting Socket through WrappedTcpClient override");
                WrappedTcpListenerAsyncResult result = _Results.Dequeue();
                result.Setup(state, _waitHandle, callback);
                _thread = new Thread(new ParameterizedThreadStart(_BackgroundAccept));
                _thread.IsBackground = true;
                _thread.Start(result);
                return result;
            }
        }

        public Socket EndAcceptSocket(IAsyncResult asyncResult)
        {
            if (!_override)
                return _listener.EndAcceptSocket(asyncResult);
            else
            {
                if (asyncResult == null)
                    throw new Exception("Unable to handle null async result.");
                WrappedTcpListenerAsyncResult result = (WrappedTcpListenerAsyncResult)asyncResult;
                Socket ret = result.Socket;
                if (((int)(_thread.ThreadState & ThreadState.Unstarted) != (int)ThreadState.Unstarted)
                        && ((int)(_thread.ThreadState & ThreadState.Stopped) != (int)ThreadState.Stopped))
                {
                    try
                    {
                        _thread.Abort();
                    }
                    catch (Exception e) {
                        Logger.LogError(e);
                    }
                }
                result.Reset();
                _Results.Enqueue(result);
                return ret;
            }
        }

        public void Stop()
        {
            if (_override)
            {
                _closed = true;
                if (_thread != null)
                {
                    if (((int)(_thread.ThreadState & ThreadState.Unstarted) != (int)ThreadState.Unstarted)
                        && ((int)(_thread.ThreadState & ThreadState.Stopped) != (int)ThreadState.Stopped))
                    {
                        try
                        {
                            _thread.Abort();
                        }
                        catch (Exception e) { }
                    }
                    _thread = null;
                }
            }
            _listener.Stop();
        }
    }
}
