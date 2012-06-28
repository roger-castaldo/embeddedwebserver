using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Org.Reddragonit.EmbeddedWebServer.Attributes;
using System.Reflection;
using Org.Reddragonit.EmbeddedWebServer.Interfaces;
using Org.Reddragonit.EmbeddedWebServer.Diagnostics;

namespace Org.Reddragonit.EmbeddedWebServer
{
    /*
         * Structure designed to store the background call information, including the referenced type,
         * and the method to be called.
         */
    public struct sCall
    {
        private BackgroundOperationCall _att;
        public BackgroundOperationCall Att
        {
            get { return _att; }
        }

        private Type _type;
        public Type type
        {
            get { return _type; }
        }

        private MethodInfo _method;
        public MethodInfo Method
        {
            get { return _method; }
        }

        public sCall(Type t, BackgroundOperationCall att, MethodInfo method)
        {
            _att = att;
            _method = method;
            _type = t;
        }
    }

    /*
     * This class runs a background thread that is similar to cron in linux.
     * It scans for implementations of IBackgroundOperationContainer and any methods contained therein 
     * tagged with a BackgroundOperationCall Attribute.  Using the BackgroundOperationCall Attribute 
     * it then determines if it should call the function as the thread runs through the information 
     * every minute.  When it finishes calling the appropriate functions the thread then sleeps for 
     * 1 minute and starts again.
     */
    internal class BackgroundOperationRunner
    {

        //milliseconds that the background thread sleeps in between runs
        private const int THREAD_SLEEP = 60000;

        //background thread to processs required method
        private Timer _runner;
        //flag to tell the background thread to finish up
        private bool _exit;
        //used to generate ids
        private MT19937 _rand;
        //houses all background calls
        private List<sCall> _calls;
        //houses all the pre calls
        private List<ServerControl.delPreBackgroundCall> _preCalls;
        public void RegisterPreCall(ServerControl.delPreBackgroundCall call)
        {
            lock (_preCalls)
            {
                _preCalls.Add(call);
            }
        }
        public void UnregisterPreCall(ServerControl.delPreBackgroundCall call)
        {
            lock (_preCalls)
            {
                _preCalls.Remove(call);
            }
        }
        //houses all the post calls
        private List<ServerControl.delPostBackgroundCall> _postCalls;
        public void RegisterPostCall(ServerControl.delPostBackgroundCall call)
        {
            lock (_postCalls)
            {
                _postCalls.Add(call);
            }
        }
        public void UnregisterPostCall(ServerControl.delPostBackgroundCall call)
        {
            lock (_postCalls)
            {
                _postCalls.Remove(call);
            }
        }

        public BackgroundOperationRunner() {
            _preCalls = new List<ServerControl.delPreBackgroundCall>();
            _postCalls = new List<ServerControl.delPostBackgroundCall>();
            _rand = new MT19937(DateTime.Now.Ticks);
            _calls = new List<sCall>();
            Logger.LogMessage(DiagnosticsLevels.TRACE, "Constructing list of background operation calls");
            foreach (Type t in Utility.LocateTypeInstances(typeof(IBackgroundOperationContainer)))
            {
                foreach (MethodInfo mi in t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    Logger.LogMessage(DiagnosticsLevels.TRACE, "Checking method " + mi.Name + " from type " + t.FullName + " for background tags");
                    foreach (BackgroundOperationCall boc in mi.GetCustomAttributes(typeof(BackgroundOperationCall), false))
                        _calls.Add(new sCall(t, boc, mi));
                }
            }
            Logger.LogMessage(DiagnosticsLevels.TRACE, "Background caller ready with " + _calls.Count.ToString() + " calls available");
        }

        //Called to start the background thread
        public void Start()
        {
            Logger.LogMessage(DiagnosticsLevels.TRACE, "Starting up background operation caller");
            _runner = new Timer(new TimerCallback(_ProcessBackgroundOperations), null, THREAD_SLEEP, THREAD_SLEEP);
            Logger.LogMessage(DiagnosticsLevels.TRACE, "Background operation caller's thread started");
        }

        private void _ProcessBackgroundOperations(object pars)
        {
            try
            {
                DateTime _start = DateTime.Now;
                Logger.LogMessage(DiagnosticsLevels.TRACE, "Checking to see which operations need to run at " + _start.ToLongDateString() + " " + _start.ToLongTimeString());
                Logger.LogMessage(DiagnosticsLevels.TRACE, "Checking against background call list of size " + _calls.Count.ToString());
                foreach (sCall sc in _calls)
                {
                    if (sc.Att.CanRunNow(_start))
                    {
                        bool run = true;
                        lock (_preCalls)
                        {
                            foreach (ServerControl.delPreBackgroundCall call in _preCalls)
                            {
                                call.Invoke(sc, ref run);
                                if (!run)
                                    break;
                            }
                        }
                        if (run)
                        {
                            ServerControl.delPostBackgroundCall[] backs;
                            lock (_postCalls)
                            {
                                backs = _postCalls.ToArray();
                            }
                            try
                            {
                                new BackgroundOperationRun(sc, _start, _rand.NextLong()).Start(backs);
                            }
                            catch (Exception e)
                            {
                                Logger.LogError(e);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogError(e);
            }
            Logger.LogMessage(DiagnosticsLevels.TRACE, "Memory prior to GC: " + GC.GetTotalMemory(false).ToString() + "b");
            try
            {
                GC.Collect();
            }
            catch (Exception e)
            {
                Logger.LogError(e);
            }
            Logger.LogMessage(DiagnosticsLevels.TRACE, "Memory after to GC: " + GC.GetTotalMemory(false).ToString() + "b");
        }

        //Called to stop the background thread
        public void Stop()
        {
            _runner.Dispose();
            _runner = null;
        }

        private delegate void delInvokeRuns(List<sCall> calls);
    }

    /*
     * This class is used to actually process all the background calls to run, it was created for
     * simplifying some code ie removing delegates as well as to allow for some more detailed logging 
     * capabilities.
     * 
     */
    internal class BackgroundOperationRun
    {
        [ThreadStatic()]
        private static BackgroundOperationRun _current = null;
        public static BackgroundOperationRun Current
        {
            get { return _current; }
        }

        private long _id;
        public long ID
        {
            get { return _id; }
        }

        private sCall _call;
        public sCall Call
        {
            get { return _call; }
        }
        private DateTime _start;
        private Thread _runner;
        private Timer _timer;

        public BackgroundOperationRun(sCall call,DateTime start,long id)
        {
            _timer = null;
            _call = call;
            _id = id;
            _start = start;
            _runner = new Thread(new ParameterizedThreadStart(_Start));
            _runner.IsBackground = true;
        }

        public void Start(ServerControl.delPostBackgroundCall[] backs)
        {
            if (_call.Att.MaxRunTime.HasValue)
                _timer = new Timer(new TimerCallback(_timedOut), null, _call.Att.MaxRunTime.Value, Timeout.Infinite);
            _runner.Start(backs);
        }

        private void _Start(object pars)
        {
            ServerControl.delPostBackgroundCall[] calls = (ServerControl.delPostBackgroundCall[])pars;
            _current = this;
            bool aborted = false;
            Exception error = null;
            DateTime start = DateTime.Now;
            Logger.LogMessage(DiagnosticsLevels.TRACE, "Invoking background operation.");
            try
            {
                _call.Method.Invoke(null, new object[] { });
            }
            catch (ThreadAbortException tae)
            {
                aborted = true;
                Logger.LogMessage(DiagnosticsLevels.CRITICAL, "Background operation call " + _call.type.FullName + "." + _call.Method.Name + " timed out");
            }
            catch (Exception e)
            {
                error = e;
                Logger.LogError(e);
            }
            double milliSeconds = DateTime.Now.Subtract(start).TotalMilliseconds;
            Logger.LogMessage(DiagnosticsLevels.TRACE, "Background operation completed.");
            if (_timer != null)
                _timer.Dispose();
            foreach (ServerControl.delPostBackgroundCall call in calls)
            {
                try
                {
                    call.Invoke(_call,milliSeconds,error,aborted);
                }
                catch (Exception e) {
                    Logger.LogError(e);
                }
            }
        }

        private void _timedOut(object pars)
        {
            _runner.Abort();
        }
    }
}