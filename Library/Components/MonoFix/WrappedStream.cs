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
        private static readonly ObjectPool<WrappedStreamAsyncResult> _Results = new ObjectPool<WrappedStreamAsyncResult>(() => new WrappedStreamAsyncResult());

        private const int _READ_TIMEOUT = 5000;
        private bool _override;
        private Stream _stream;
        private ManualResetEvent _waitHandle;
        private bool _closed;
        private Thread _thread;

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
                _thread = null;
            }
        }

        internal void AuthenticateAsServer(X509Certificate2 certificate)
        {
            ((SslStream)_stream).AuthenticateAsServer(certificate);
        }

        private void _BackgroundRead(object obj)
        {
            WrappedStreamAsyncResult result = (WrappedStreamAsyncResult)obj;
            int readCount = 0;
            try
            {
                readCount = _stream.Read(result.Buffer, result.Index, result.Len);
            }
            catch (ThreadAbortException tae)
            {
                result.Reset();
                _Results.Enqueue(result);
                result = null;
            }
            catch (Exception e)
            {
                readCount = 0;
            }
            if (!_closed)
            {
                result.Complete(readCount);
                _waitHandle.Set();
                ThreadPool.QueueUserWorkItem(new WaitCallback(_ProcCallBack),result);
            }
        }

        private void _ProcCallBack(object state)
        {
            if ((int)(_thread.ThreadState & ThreadState.Stopped) != (int)ThreadState.Stopped)
            {
                try
                {
                    _thread.Join();
                }
                catch (Exception e) { }
            }
            WrappedStreamAsyncResult result = (WrappedStreamAsyncResult)state;
            try
            {
                if (result.CallBack != null)
                    result.CallBack(result);
            }
            catch (Exception e) { }
        }

        internal IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callBack,object state)
        {
            if (!_override)
            {
                try
                {
                    return _stream.BeginRead(buffer, offset, count, callBack, state);
                }
                catch (Exception e) {
                    return null;
                }
            }
            else
            {
                if (_thread != null)
                {
                    if (((int)(_thread.ThreadState & ThreadState.Unstarted) != (int)ThreadState.Unstarted)
                        && ((int)(_thread.ThreadState & ThreadState.Stopped) != (int)ThreadState.Stopped))
                        throw new Exception("Unable to Begin Reading, already async reading.");
                }
                WrappedStreamAsyncResult result = _Results.Dequeue();
                result.Start(state, _waitHandle, callBack, offset, buffer, count);
                _thread = new Thread(new ParameterizedThreadStart(_BackgroundRead));
                _thread.IsBackground = true;
                _thread.Start(result);
                return result;
            }
        }

        internal int EndRead(IAsyncResult asyncResult)
        {
            if (!_override)
                return _stream.EndRead(asyncResult);
            else
            {
                if (asyncResult == null)
                    throw new Exception("Unable to handle null async result.");
                WrappedStreamAsyncResult result = (WrappedStreamAsyncResult)asyncResult;
                int ret = result.Len;
                result.Reset();
                _Results.Enqueue(result);
                return ret;
            }
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
            _stream.Dispose();
        }

        #endregion
    }
}
