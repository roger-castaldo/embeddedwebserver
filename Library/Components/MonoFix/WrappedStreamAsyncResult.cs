using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Org.Reddragonit.EmbeddedWebServer.Components.MonoFix
{
    internal class WrappedStreamAsyncResult : IAsyncResult
    {
        private int _len;
        public int Len
        {
            get { return _len; }
        }

        private AsyncCallback _callBack;
        public AsyncCallback CallBack
        {
            get { return _callBack; }
        }

        private byte[] _buffer;
        public byte[] Buffer{
            get { return _buffer; }
            set { _buffer = value; }
        }

        private int _index;
        public int Index
        {
            get { return _index; }
        }

        public WrappedStreamAsyncResult(){
        }

        public void Start(object asyncState, WaitHandle waitHandle,AsyncCallback callback,int index,byte[] buffer,int len)
        {
            _asyncState = asyncState;
            _waitHandle = waitHandle;
            _len = len;
            _index = index;
            _buffer = buffer;
            _callBack = callback;
        }

        public void Reset()
        {
            _asyncState = null;
            _waitHandle = null;
            _isCompleted = false;
            _completedSynchronously = false;
            _len = 0;
            _index = 0;
            _buffer = null;
        }

        internal void CompleteSynchronously()
        {
            _completedSynchronously = true;
            _isCompleted = true;
        }

        internal void Complete(int len)
        {
            _isCompleted = true;
            _len = len;
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
