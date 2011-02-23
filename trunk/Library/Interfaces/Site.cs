using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using Org.Reddragonit.EmbeddedWebServer.Components;
using Org.Reddragonit.EmbeddedWebServer.BasicHandlers;
using Org.Reddragonit.EmbeddedWebServer.Sessions;
using System.Threading;
using Org.Reddragonit.EmbeddedWebServer.Attributes;

namespace Org.Reddragonit.EmbeddedWebServer.Interfaces
{
    public abstract class Site : IBackgroundOperationContainer
    {

        #region virtual
        public virtual int Port
        {
            get { return 80; }
        }

        public virtual IPAddress IPToListenTo
        {
            get { return IPAddress.Any; }
        }

        public virtual string ServerName
        {
            get { return null; }
        }

        public virtual bool AllowPOST
        {
            get { return true; }
        }

        public virtual bool AllowGET
        {
            get { return true; }
        }

        public virtual SiteSessionTypes SessionStateType
        {
            get { return SiteSessionTypes.None; }
        }

        public virtual string TMPPath
        {
            get { return "/tmp"; }
        }

        public virtual string BaseSitePath
        {
            get { return null; }
        }

        public virtual List<sEmbeddedFile> EmbeddedFiles
        {
            get { return null; }
        }

        public virtual int SessionTimeoutMinutes
        {
            get { return 60; }
        }

        public virtual int? CookieExpireMinutes
        {
            get { return null; }
        }

        private static readonly IRequestHandler[] _defaultHandlers = new IRequestHandler[]{
            new EmbeddedResourceHandler(),
            new FileHandler()
        };

        public virtual List<IRequestHandler> Handlers
        {
            get
            {
                return new List<IRequestHandler>(_defaultHandlers);
            }
        }

        public virtual int CacheItemExpiryMinutes
        {
            get { return 60; }
        }

        public virtual void Start()
        {
        }

        public virtual void Stop()
        {
        }
        #endregion

        private object _lock = new object();
        private Dictionary<string, CachedItemContainer> _cache;

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
                Monitor.Exit(_lock);
            }
        }

        public void ProcessRequest(HttpConnection conn)
        {
            bool found = false;
            foreach (IRequestHandler handler in Handlers)
            {
                if (handler.CanProcessRequest(conn, this))
                {
                    found = true;
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
                    if (handler.RequiresSessionForRequest(conn,this))
                        SessionManager.StoreSessionForConnection(conn, this);
                    break;
                }
            }
            if (!found)
            {
                conn.ClearResponse();
                conn.ResponseStatus = HttpStatusCodes.Not_Found;
            }
            conn.SendResponse();
        }

        public Site() {
            _cache = new Dictionary<string, CachedItemContainer>();
        }
    }
}
