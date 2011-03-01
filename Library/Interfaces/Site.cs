using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using Org.Reddragonit.EmbeddedWebServer.Components;
using Org.Reddragonit.EmbeddedWebServer.BasicHandlers;
using Org.Reddragonit.EmbeddedWebServer.Sessions;
using System.Threading;
using Org.Reddragonit.EmbeddedWebServer.Attributes;
using Org.Reddragonit.EmbeddedWebServer.Diagnostics;
using System.IO;

namespace Org.Reddragonit.EmbeddedWebServer.Interfaces
{
    public abstract class Site : IBackgroundOperationContainer
    {
        //Statically holds the current site being used in the current thread
        [ThreadStatic()]
        private static Site _currentSite;
        public static Site CurrentSite
        {
            get { return _currentSite; }
        }

        //called by the handler at the start of the asyncrhonous process to tag which is the current site for the 
        //currently running thread
        internal static void SetCurrentSite(Site site)
        {
            _currentSite = site;
        }

        #region virtual
        //what port the site listens on
        public virtual int Port
        {
            get { return 80; }
        }

        //what IP to bind the tcp listener to for the site
        public virtual IPAddress IPToListenTo
        {
            get { return IPAddress.Any; }
        }

        //The host name that the site represents
        public virtual string ServerName
        {
            get { return null; }
        }

        //indicates if the site should allow post
        public virtual bool AllowPOST
        {
            get { return true; }
        }

        //indicates if the site should allow get
        public virtual bool AllowGET
        {
            get { return true; }
        }

        //indicates the type of session storage that the site uses, defaults to no sessions
        public virtual SiteSessionTypes SessionStateType
        {
            get { return SiteSessionTypes.None; }
        }

        //indicates the temporary path that the site uses to store information
        public virtual string TMPPath
        {
            get { return "/tmp"; }
        }

        //indicates the base file system path to use for the site 
        public virtual string BaseSitePath
        {
            get { return null; }
        }

        //returns the list of embedded files available for the site
        public virtual List<sEmbeddedFile> EmbeddedFiles
        {
            get { return null; }
        }

        //indicates the time to hold a session before expiring it
        public virtual int SessionTimeoutMinutes
        {
            get { return 60; }
        }

        //indicates the time to hold a cookie for the site.  If not set, it assumes 60 minute default
        public virtual int CookieExpireMinutes
        {
            get { return Org.Reddragonit.EmbeddedWebServer.Components.CookieCollection.DEFAULT_COOKIE_DURATION_MINUTES; }
        }

        private static readonly IRequestHandler[] _defaultHandlers = new IRequestHandler[]{
            new EmbeddedServiceHandler(),
            new EmbeddedResourceHandler(),
            new FileHandler()
        };

        //returns the list of handlers that the site uses
        public virtual List<IRequestHandler> Handlers
        {
            get
            {
                return new List<IRequestHandler>(_defaultHandlers);
            }
        }

        //indicates the amount of minutes a cached item should be cached for
        public virtual int CacheItemExpiryMinutes
        {
            get { return 60; }
        }

        //Indicates the available embedded services that the site uses
        public virtual List<Type> EmbeddedServiceTypes
        {
            get
            {
                return Utility.LocateTypeInstances(typeof(EmbeddedService));
            }
        }

        //indicates to add the json javascript with a service javascript request.
        //set true if the pages involved don't reference the json javascript by default
        public bool AddJsonJavascript
        {
            get { return true; }
        }

        //indicates to add the jquery javascript with a service javascript request.
        //set true if the pages involved don't reference the jquery javascript by default
        public bool AddJqueryJavascript
        {
            get { return true; }
        }

        //indicates the diagnostics level the site should use for logging
        public virtual DiagnosticsLevels DiagnosticsLevel
        {
            get{ return Settings.DiagnosticsLevel; }
        }

        //indicates the location to write these logs to
        public virtual DiagnosticsOutputs DiagnosticsOutput
        {
            get { return Settings.DiagnosticsOutput; }
        }

        //indicates the port to bind the logging socket to
        public virtual IPEndPoint RemoteLoggingServer
        {
            get { return new IPEndPoint(IPAddress.Broadcast,8081); }
        }

        //an implemented start that gets called before the handlers are initialized
        protected virtual void PreStart(){}
        //an implemented start that gets called after all the handlers are initialized
        protected virtual void PostStart(){}
        //an implemented stop that gets called before the handlers are deinitialized
        protected virtual void PreStop() { }
        //an implemented stop that gets called after the handlers are deinitialized
        protected virtual void PostStop() { }
        //an implemented function that gets called before the processing of a request
        protected virtual void PreRequest(HttpConnection conn) { }
        //an implemented function that gets called after the processing of a request
        protected virtual void PostRequest(HttpConnection conn) { }
        #endregion

        //Called to start the site up, runs all required initializations
        public void Start()
        {
            PreStart();
            _currentSite = this;
            foreach (IRequestHandler handler in Handlers)
            {
                handler.Init();
            }
            PostStart();
        }

        //Called to stop the site, runs all deinits and the internal stop function
        public void Stop()
        {
            PreStop();
            _currentSite = this;
            foreach (IRequestHandler handler in Handlers)
            {
                handler.DeInit();
            }
            PostStop();
        }

        //used to lock the site cache object for thread safe access
        private object _lock = new object();
        //The cache designed to hold 
        private Dictionary<string, CachedItemContainer> _cache;

        /*
         * This function is a background threaded operation that every minute
         * will check the cache to see what, if any items needs to be cleaned
         * for each site that is loaded in the server control.  Once all caches have 
         * been cleaned, it calls the Garbage Collector in order to attempt
         * a forced memory release.
         */
        [BackgroundOperationCall(-1, -1, -1, -1, BackgroundOperationDaysOfWeek.All)]
        public static void CleanCaches()
        {
            List<Site> sites = ServerControl.Sites;
            foreach (Site site in sites)
            {
                Monitor.Enter(site._lock);
                string[] keys = new string[site._cache.Count];
                site._cache.Keys.CopyTo(keys,0);
                foreach (string str in keys)
                {
                    if (DateTime.Now.Subtract(site._cache[str].LastAccess).TotalMinutes <= site.CacheItemExpiryMinutes)
                        site._cache.Remove(str);
                }
                Monitor.Exit(site._lock);
            }
            GC.Collect();
        }

        //This property is used to return an object contained within the cache,
        //or add/update one in the cache
        public object this[string cachedItemName]
        {
            get
            {
                object ret = null;
                Monitor.Enter(_lock);
                if (_cache.ContainsKey(cachedItemName))
                    ret = _cache[cachedItemName].Value;
                Monitor.Exit(_lock);
                return ret;
            }
            set
            {
                Monitor.Enter(_lock);
                if (_cache.ContainsKey(cachedItemName))
                    _cache[cachedItemName].Value = value;
                else
                    _cache.Add(cachedItemName, new CachedItemContainer(value));
                Monitor.Exit(_lock);
            }
        }

        public List<string> CachedItemKeys
        {
            get{
                Monitor.Enter(_lock);
                string[] tmp = new string[_cache.Count];
                _cache.Keys.CopyTo(tmp, 0);
                Monitor.Exit(_lock);
                return new List<string>(tmp);
            }
        }

        /*
         * This function is called by the port listener once the appropriate site has
         * been located to process the given request.  It scans through all 
         * available handlers and attempts to find the appropriate one.  Once 
         * it has found the appropriate handler, it flags this fact, then 
         * checks if the handler is reusable.  If so, run the request through, checking for
         * the requirement of a session first.  If not, create a new instance, init the instance, and use the new instance 
         * to perform the same tasks, once completed deinit the handler.
         */
        public void ProcessRequest(HttpConnection conn)
        {
            PreRequest(conn);
            _currentSite = this;
            bool found = false;
            foreach (IRequestHandler handler in Handlers)
            {
                if (handler.CanProcessRequest(conn, this))
                {
                    found = true;
                    if (handler.IsReusable)
                    {
                        if (handler.RequiresSessionForRequest(conn, this))
                            SessionManager.LoadStateForConnection(conn, this);
                        try
                        {
                            handler.ProcessRequest(conn, this);
                        }
                        catch (Exception e)
                        {
                            conn.ResponseStatus = HttpStatusCodes.Internal_Server_Error;
                            conn.ClearResponse();
                            conn.ResponseWriter.Write(e.Message);
                        }
                        if (handler.RequiresSessionForRequest(conn, this))
                            SessionManager.StoreSessionForConnection(conn, this);
                    }
                    else
                    {
                        IRequestHandler hndl = (IRequestHandler)handler.GetType().GetConstructor(Type.EmptyTypes).Invoke(new object[0]);
                        hndl.Init();
                        if (hndl.RequiresSessionForRequest(conn, this))
                            SessionManager.LoadStateForConnection(conn, this);
                        try
                        {
                            hndl.ProcessRequest(conn, this);
                        }
                        catch (Exception e)
                        {
                            conn.ResponseStatus = HttpStatusCodes.Internal_Server_Error;
                            conn.ClearResponse();
                            conn.ResponseWriter.Write(e.Message);
                        }
                        if (hndl.RequiresSessionForRequest(conn, this))
                            SessionManager.StoreSessionForConnection(conn, this);
                        hndl.DeInit();
                    }
                    break;
                }
            }
            if (!found)
            {
                conn.ClearResponse();
                conn.ResponseStatus = HttpStatusCodes.Not_Found;
            }
            PostRequest(conn);
            conn.SendResponse();
        }

        //in the class creator init the cache object
        public Site() {
            _cache = new Dictionary<string, CachedItemContainer>();
        }

        public string MapPath(string path)
        {
            if (BaseSitePath != null)
                return BaseSitePath + Path.DirectorySeparatorChar + path.Replace('/', Path.DirectorySeparatorChar);
            return null;
        }
    }
}
