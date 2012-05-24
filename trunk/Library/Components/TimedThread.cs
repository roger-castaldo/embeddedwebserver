using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Timer = System.Timers.Timer;
using System.Timers;

namespace Org.Reddragonit.EmbeddedWebServer.Components
{
    public class TimedThread
    {
        private Timer _timMonitor;
        private Thread _opThread;
        private bool _timedout = false;
        private ManualResetEvent _mre;
        private Exception _exception=null;
        private bool _exceptionOnTimeout;
        private bool _aborted=false;
        private int _timeout;
        private bool _waiting = false;

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

        public void Start(bool exceptionOnTimeout)
        {
            Start(int.MaxValue,exceptionOnTimeout);
        }

        public void Start(int timeout)
        {
            Start(timeout,true);
        }

        public void Start(int timeout,bool exceptionOnTimeout)
        {
            _timeout = timeout;
            _exceptionOnTimeout = exceptionOnTimeout;
            _timMonitor = new Timer(timeout);
            _timMonitor.AutoReset = false;
            _timMonitor.Elapsed += new ElapsedEventHandler(_threadTimeout);
            try
            {
                _opThread.Start(null);
            }
            catch (ThreadAbortException tae) { }
            catch (Exception ex)
            {
                if (_waiting)
                    _exception = ex;
                else
                    throw ex;
            }
            _timMonitor.Start();
        }

        public void Start(object parameter)
        {
            Start(parameter,int.MaxValue,true);
        }

        public void Start(object parameter,bool exceptionOnTimeout)
        {
            Start(parameter, int.MaxValue, exceptionOnTimeout);
        }

        public void Start(object parameter,int timeout)
        {
            Start(parameter, timeout,true);
        }

        public void Start(object parameter,int timeout,bool exceptionOnTimeout)
        {
            _timeout = timeout;
            _exceptionOnTimeout = exceptionOnTimeout;
            _timMonitor = new Timer(timeout);
            _timMonitor.AutoReset = false;
            _timMonitor.Elapsed += new System.Timers.ElapsedEventHandler(_threadTimeout);
            try
            {
                _opThread.Start(parameter);
            }
            catch (ThreadAbortException tae) { }
            catch (Exception ex)
            {
                if (_waiting)
                    _exception = ex;
                else
                    throw ex;
            }
            _timMonitor.Start();
        }

        private void _threadTimeout(object sender, ElapsedEventArgs e)
        {
            if (!_aborted)
            {
                try
                {
                    _opThread.Abort();
                }
                catch (Exception ex)
                {
                }
                _timedout = true;
            }
            if (_timedout && _exceptionOnTimeout && !_waiting)
                throw new ThreadTimeoutException(_timeout);
            else
                _mre.Set();
        }

        public void Abort()
        {
            _aborted = true;
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
            _waiting = true;
            _mre.WaitOne();
            timedOut = _timedout;
            exception = _exception;
        }

        public bool IsBackground
        {
            get { return _opThread.IsBackground; }
            set { _opThread.IsBackground = value; }
        }
    }
}
