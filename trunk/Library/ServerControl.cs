using System;
using System.Collections.Generic;
using System.Text;
using Org.Reddragonit.EmbeddedWebServer.Interfaces;
using Org.Reddragonit.EmbeddedWebServer.Components;
using System.Threading;
using Org.Reddragonit.EmbeddedWebServer.Sessions;
using Org.Reddragonit.EmbeddedWebServer.Diagnostics;

namespace Org.Reddragonit.EmbeddedWebServer
{
    public class ServerControl
    {
        /*
         * This is the main exposed class.  It is used to control the web server itself by calling to
         * either start or stop the server.
         */

        //used as a single use lock for the lsits below
        private static object _lock=new object();
        //the list of Port Listeners that were created to run on the server
        private static List<PortListener> _listeners;
        //flag to indicate if the server has been started.
        private static bool _started = false;
        //the background operation control that gets started when the servers are started.
        //it runs similarly to the linux cron concept.
        private static BackgroundOperationRunner _backgroundRunner;
        
        //Returns a full list of site implementations loaded by the server controller.
        public static List<Site> Sites
        {
            get
            {
                List<Site> ret = new List<Site>();
                Monitor.Enter(_lock);
                if (_listeners!=null){
                    foreach (PortListener pt in _listeners)
                    {
                        ret.AddRange(pt.Sites);
                    }
                }
                Monitor.Exit(_lock);
                return ret;
            }
        }

        //Returns the status of the server control, as to whether its started or not.
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

        /*
         * Starts the server processes.  This function scans for all implementations of the Site interface.
         * Following that, it then creates the required Tcp Listeners for the sites found.
         * One all listeners have been created and the sites loaded into them appropriately,
         * start all listeners which starts all sites.  Following that, start the background runner 
         * thread to handle all cron jobs that run in the background.
         */
        public static void Start()
        {
            Monitor.Enter(_lock);
            if (_started)
                throw new Exception(Messages.Current["Org.Reddragonit.EmbeddedWebServer.ServerControl.Errors.ServerStarted"]);
            else
            {
                MT19937 _rand = new MT19937(DateTime.Now.Ticks);
                _listeners = new List<PortListener>();
                foreach (Type t in Utility.LocateTypeInstances(typeof(Site)))
                {
                    Site s = (Site)t.GetConstructor(Type.EmptyTypes).Invoke(new object[] { });
                    s.ID = SessionManager.GenerateSessionID();
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
                foreach (PortListener pt in _listeners)
                    pt.Start();
                _backgroundRunner = new BackgroundOperationRunner();
                _backgroundRunner.Start();
                _started = true;
            }
            Monitor.Exit(_lock);
        }

        /*
         * Stops all server processes.  This function stops each of the port listener objects, which in turn
         * stops all of the sites contained within the listener.  This is followed by stopping the background process runner 
         * and committing out the log (which dumps any remaining queued up messages to the file system is file based logging is enabled.)
         */
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
                _backgroundRunner.Stop();
                _listeners = null;
                _started = false;
            }
            Logger.CleanupRemainingMessages();
            Monitor.Exit(_lock);
        }
    }
}
