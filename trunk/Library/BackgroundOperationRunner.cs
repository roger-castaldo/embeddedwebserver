using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Org.Reddragonit.EmbeddedWebServer.Attributes;
using System.Reflection;
using Org.Reddragonit.EmbeddedWebServer.Interfaces;

namespace Org.Reddragonit.EmbeddedWebServer
{
    internal class BackgroundOperationRunner
    {
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

        private const int THREAD_SLEEP = 60000;

        private delegate void InvokeMethod();

        private Thread _runner;
        private bool _exit;

        public BackgroundOperationRunner() { }

        public void Start()
        {
            _exit = false;
            _runner = new Thread(new ThreadStart(RunThread));
            _runner.Start();
        }

        public void Stop()
        {
            _exit = true;
            try
            {
                _runner.Interrupt();
            }
            catch (Exception e) { }
            try
            {
                _runner.Join();
            }
            catch (Exception e)
            {
            }
        }

        private void RunThread()
        {
            List<sCall> calls = new List<sCall>();
            foreach (Type t in Utility.LocateTypeInstances(typeof(IBackgroundOperationContainer)))
            {
                foreach (MethodInfo mi in t.GetMethods(BindingFlags.Static | BindingFlags.Public))
                {
                    if (mi.GetCustomAttributes(typeof(BackgroundOperationCall),false).Length > 0)
                    {
                        calls.Add(new sCall(t,(BackgroundOperationCall)mi.GetCustomAttributes(typeof(BackgroundOperationCall),false)[0], mi));
                    }
                }
            }
            while (!_exit)
            {
                DateTime dt = DateTime.Now;
                foreach (sCall call in calls)
                {
                    if (_exit)
                        break;
                    ((InvokeMethod)InvokeMethod.CreateDelegate(typeof(InvokeMethod), call.Method)).BeginInvoke(new AsyncCallback(InvokeFinish), null);
                }
                if (!_exit)
                {
                    try
                    {
                        Thread.Sleep(THREAD_SLEEP);
                    }
                    catch (Exception e) { }
                }
            }
        }

        private void InvokeFinish(IAsyncResult res)
        {
        }
    }
}
