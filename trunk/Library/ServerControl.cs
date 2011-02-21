using System;
using System.Collections.Generic;
using System.Text;
using Org.Reddragonit.EmbeddedWebServer.Interfaces;
using Org.Reddragonit.EmbeddedWebServer.Components;
using System.Threading;

namespace Org.Reddragonit.EmbeddedWebServer
{
    public class ServerControl
    {
        private static object _lock=new object();
        private static List<PortListener> _listeners;
        private static bool _started = false;

        public static bool IsStarted
        {
            get
            {
                bool ret = false;
                Monitor.Enter(_lock);
                ret = _started;
                Monitor.Exit(_lock);
                return ret;
            }
        }

        public static void Start()
        {
            Monitor.Enter(_lock);
            if (_started)
                throw new Exception(Messages.Current["Org.Reddragonit.EmbeddedWebServer.ServerControl.Errors.ServerStarted"]);
            else
            {
                _listeners = new List<PortListener>();
                foreach (Type t in Utility.LocateTypeInstances(typeof(Site)))
                {
                    Site s = (Site)t.GetConstructor(Type.EmptyTypes).Invoke(new object[] { });
                    bool add = true;
                    foreach (PortListener pt in _listeners)
                    {
                        if (pt.Port == s.Port)
                        {
                            pt.AttachSite(s);
                            add = false;
                        }
                    }
                    if (add)
                        _listeners.Add(new PortListener(s));
                }
                _started = true;
            }
            Monitor.Exit(_lock);
        }

        public static void Stop()
        {
            Monitor.Enter(_lock);
            if (!_started)
                throw new Exception(Messages.Current["Org.Reddragonit.EmbeddedWebServer.ServerControl.Errors.ServerNotStarted"]);
            else
            {
                foreach (PortListener pt in _listeners)
                {
                    try
                    {
                        pt.Stop();
                    }
                    catch (Exception e)
                    {
                    }
                }
                _listeners = null;
                _started = false;
            }
            Monitor.Exit(_lock);
        }
    }
}
