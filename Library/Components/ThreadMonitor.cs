using System;
using System.Collections.Generic;
using System.Text;
using Org.Reddragonit.EmbeddedWebServer.Interfaces;
using System.Threading;
using Org.Reddragonit.EmbeddedWebServer.Attributes;

namespace Org.Reddragonit.EmbeddedWebServer.Components
{
    public class ThreadMonitor : IBackgroundOperationContainer
    {
        private struct sMonitoredThread
        {
            private DateTime _expires;
            private Thread _thread;

            public bool IsDone
            {
                get
                {
                    if (_thread == null)
                        return true;
                    return _thread.ThreadState == ThreadState.Running || _thread.ThreadState == ThreadState.Suspended || _thread.ThreadState == ThreadState.WaitSleepJoin;
                }
            }

            public bool IsExpired
            {
                get { return DateTime.Now.Ticks > _expires.Ticks; }
            }

            public void Abort()
            {
                try { _thread.Abort(); }
                catch (Exception e) { }
            }

            public sMonitoredThread(Thread thread, DateTime expires)
            {
                _thread = thread;
                _expires = expires;
            }

            public override bool Equals(object obj)
            {
                if (obj is sMonitoredThread)
                    return ((sMonitoredThread)obj)._thread.ManagedThreadId == _thread.ManagedThreadId;
                return false;
            }
        }

        private static List<sMonitoredThread> _threads;

        static ThreadMonitor()
        {
            _threads = new List<sMonitoredThread>();
        }

        [BackgroundOperationCall(-1, -1, -1, -1, BackgroundOperationDaysOfWeek.All)]
        internal void CheckThreads()
        {
            lock(_threads){
                for (int x = 0; x < _threads.Count; x++)
                {
                    if (_threads[x].IsDone)
                    {
                        _threads.RemoveAt(x);
                        x--;
                    }
                    else if (_threads[x].IsExpired)
                    {
                        _threads[x].Abort();
                        _threads.RemoveAt(x);
                        x--;
                    }
                }
            }
        }

        public static void MonitorThread(Thread thread, int milliseconds)
        {
            lock (_threads)
            {
                _threads.Add(new sMonitoredThread(thread, DateTime.Now.AddMilliseconds(milliseconds)));
            }
        }

        public static void MonitorThread(Thread thread, DateTime timeout)
        {
            lock (_threads)
            {
                _threads.Add(new sMonitoredThread(thread, timeout));
            }
        }

        public static void AbortThreadMonitoring(Thread thread)
        {
            lock (_threads)
            {
                _threads.Remove(new sMonitoredThread(thread, DateTime.Now));
            }
        }
    }
}
