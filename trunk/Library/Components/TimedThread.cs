using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Org.Reddragonit.EmbeddedWebServer.Components
{
    public class TimedThread
    {
        private Thread _opThread;
        private Thread _timerThread;
        private bool _timedout = false;
        private ManualResetEvent _mre;
        private DateTime _endTime;
        private Exception _exception=null;

        public TimedThread(ThreadStart start)
        {
            _opThread = new Thread(start);
            _mre = new ManualResetEvent(false);
        }

        public TimedThread(ParameterizedThreadStart start)
        {
            _opThread = new Thread(start);
            _mre = new ManualResetEvent(false);
        }

        public TimedThread(ParameterizedThreadStart start,int maxStackSize)
        {
            _opThread = new Thread(start,maxStackSize);
            _mre = new ManualResetEvent(false);
        }

        public void Start()
        {
            Start(int.MaxValue);
        }

        public void Start(int timeout)
        {
            _endTime = DateTime.Now.AddMilliseconds(timeout);
            _timerThread = new Thread(new ParameterizedThreadStart(_Start));
            _timerThread.Start(timeout);
        }

        public void Start(object parameter)
        {
            Start(parameter,int.MaxValue);
        }

        public void Start(object parameter,int timeout)
        {
            _endTime = DateTime.Now.AddMilliseconds(timeout);
            _timerThread = new Thread(new ParameterizedThreadStart(_Start));
            _timerThread.Start(new object[]{parameter,timeout});
        }

        private void _Start(object pars)
        {
            int timeout = 0;
            if (pars is object[])
            {
                _opThread.Start(((object[])pars)[0]);
                timeout = (int)((object[])pars)[1];
            }
            else
            {
                _opThread.Start();
                timeout = (int)pars;
            }
            try
            {
                if (!_opThread.Join(timeout))
                {
                    _timedout = true;
                    try
                    {
                        _opThread.Abort();
                    }
                    catch (Exception ex)
                    {
                    }
                }
            }
            catch (ThreadAbortException tae)
            {
                _timedout = true;
            }
            catch (Exception e)
            {
                _exception = e;
            }
            if (_timedout)
                throw new ThreadTimeoutException(timeout);
            else
                _mre.Set();
        }

        public void Abort()
        {
            try
            {
                _timerThread.Abort();
            }
            catch (Exception e)
            {
            }
            try
            {
                _opThread.Abort();
            }
            catch (Exception e)
            {
            }
        }

        public void Join()
        {
            _mre.WaitOne();
        }

        public bool Join(int milliSeconds)
        {
            return _mre.WaitOne(milliSeconds);
        }

        public bool Join(TimeSpan span)
        {
            return _mre.WaitOne(span);
        }

        public void WaitTillFinished(out bool timedOut,out Exception exception)
        {
            timedOut = false;
            if (!_mre.WaitOne((int)_endTime.Subtract(DateTime.Now).TotalMilliseconds))
                timedOut = true;
            timedOut |= _timedout;
            exception = _exception;
        }

        public bool IsBackground
        {
            get { return _opThread.IsBackground; }
            set { _opThread.IsBackground = value; }
        }
    }
}
