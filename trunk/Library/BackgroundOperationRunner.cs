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
     * This class runs a background thread that is similar to cron in linux.
     * It scans for implementations of IBackgroundOperationContainer and any methods contained therein 
     * tagged with a BackgroundOperationCall Attribute.  Using the BackgroundOperationCall Attribute 
     * it then determines if it should call the function as the thread runs through the information 
     * every minute.  When it finishes calling the appropriate functions the thread then sleeps for 
     * 1 minute and starts again.
     */
    internal class BackgroundOperationRunner
    {
        /*
         * Structure designed to store the background call information, including the referenced type,
         * and the method to be called.
         */
        private struct sCall
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

            public sCall(Type t,BackgroundOperationCall att, MethodInfo method)
            {
                _att = att;
                _method = method;
                _type = t;
            }
        }

        //milliseconds that the background thread sleeps in between runs
        private const int THREAD_SLEEP = 60000;

        //delegate used to invoke the required background operation asynchronously.
        private delegate void InvokeMethod();

        //background thread to processs required method
        private Thread _runner;
        //flag to tell the background thread to finish up
        private bool _exit;

        public BackgroundOperationRunner() { }

        //Called to start the background thread
        public void Start()
        {
            _exit = false;
            _runner = new Thread(new ThreadStart(RunThread));
            _runner.Start();
        }

        //Called to stop the background thread
        public void Stop()
        {
            _exit = true;
            try
            {
                _runner.Interrupt();
            }
            catch (Exception e) {
                Logger.LogError(e);
            }
            try
            {
                _runner.Join();
            }
            catch (Exception e)
            {
                Logger.LogError(e);
            }
        }

        private delegate void delInvokeRuns(List<sCall> calls);

        /*
         * This is the core of the background thread concept.  It loads all instances of 
         * IBackgroundOperationContainer and locates all public static methods that 
         * are tagged with the BackgroundOperationCall Attribute.  It then loads
         * these all into a list.  While the exit flag is not set it scans all calls, checks for which need to 
         * run, produces a delegate and executes them asynchronously.
         */
        private void RunThread()
        {
            List<sCall> calls = new List<sCall>();
            foreach (Type t in Utility.LocateTypeInstances(typeof(IBackgroundOperationContainer)))
            {
                foreach (MethodInfo mi in t.GetMethods(BindingFlags.Static | BindingFlags.Public))
                {
                    foreach (BackgroundOperationCall boc in mi.GetCustomAttributes(typeof(BackgroundOperationCall), false))
                        calls.Add(new sCall(t, boc, mi));
                }
            }
            delInvokeRuns del = new delInvokeRuns(InvokeRuns);
            while (!_exit)
            {
                del.BeginInvoke(calls, new AsyncCallback(InvokeFinish), null);
                if (!_exit)
                {
                    try
                    {
                        Thread.Sleep((int)DateTime.Now.AddMilliseconds(THREAD_SLEEP).Subtract(DateTime.Now).TotalMilliseconds);
                    }
                    catch (Exception e) {
                        Logger.LogError(e);
                    }
                }
            }
        }

        private void InvokeRuns(List<sCall> calls)
        {
            DateTime dt = DateTime.Now;
            foreach (sCall call in calls)
            {
                if (_exit)
                    break;
                if (call.Att.CanRunNow(dt))
                    ((InvokeMethod)InvokeMethod.CreateDelegate(typeof(InvokeMethod), call.Method)).BeginInvoke(new AsyncCallback(InvokeFinish), null);
            }
        }

        private void InvokeFinish(IAsyncResult res)
        {
        }
    }
}
