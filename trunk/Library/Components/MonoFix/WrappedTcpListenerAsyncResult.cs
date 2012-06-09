using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Net.Sockets;

namespace Org.Reddragonit.EmbeddedWebServer.Components.MonoFix
{
    internal class WrappedTcpListenerAsyncResult : IAsyncResult
    {
        private Socket _socket;
        public Socket Socket
        {
            get { return _socket; }
        }

        private AsyncCallback _callBack;
        public AsyncCallback CallBack
        {
            get { return _callBack; }
        }

        public WrappedTcpListenerAsyncResult()
        {
        }

        public void Setup(object asyncState, WaitHandle waitHandle,AsyncCallback callback)
        {
            _asyncState = asyncState;
            _waitHandle = waitHandle;
            _callBack = callback;
        }

        internal void CompleteSynchronously()
        {
            _completedSynchronously = true;
            _isCompleted = true;
        }

        internal void Complete(Socket socket)
        {
            _isCompleted = true;
            _socket = socket;
        }

        public void Reset()
        {
            _socket = null;
            _asyncState = null;
            _waitHandle = null;
            _isCompleted = false;
            _completedSynchronously = false;
        }

        #region IAsyncResult Members

        private object _asyncState;
        public object AsyncState
        {
            get { return _asyncState; }
        }

        private WaitHandle _waitHandle;
        public WaitHandle AsyncWaitHandle
        {
            get { return _waitHandle; }
        }

        private bool _completedSynchronously;
        public bool CompletedSynchronously
        {
            get { return _completedSynchronously; }
        }

        private bool _isCompleted;
        public bool IsCompleted
        {
            get { return _isCompleted; }
        }

        #endregion
    }
}