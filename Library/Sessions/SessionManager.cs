using System;
using System.Collections.Generic;
using System.Text;
using Org.Reddragonit.EmbeddedWebServer.Components;
using Org.Reddragonit.EmbeddedWebServer.Interfaces;
using System.Threading;
using System.IO;
using Org.Reddragonit.EmbeddedWebServer.Attributes;
using Org.Reddragonit.EmbeddedWebServer.Components.Message;

namespace Org.Reddragonit.EmbeddedWebServer.Sessions
{
    internal class SessionManager : IBackgroundOperationContainer
    {
        private const string _ALLOWED_SESSION_ID_CHARS = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        private const int SESSION_ID_LEN = 12;
        private const int THREAD_SLEEP = 60000;
        private const int IP_SESSION_ID_MINUTES = 5;

        private static object _lock = new object();
        private static List<SessionState> _sessions;
        private static Dictionary<string, CachedItemContainer> _ipSessionIds;
        private static MT19937 _rand = new MT19937();

        [BackgroundOperationCall(-1,-1,-1,-1,BackgroundOperationDaysOfWeek.All)]
        public static void CleanupSessions()
        {
            Monitor.Enter(_lock);
            if (_sessions != null)
            {
                for (int x = 0; x < _sessions.Count; x++)
                {
                    if (_sessions[x].Expiry.Ticks < DateTime.Now.Ticks)
                    {
                        _sessions.RemoveAt(x);
                        x--;
                    }
                }
            }
            List<Site> sites = ServerControl.Sites;
            foreach (Site s in sites)
            {
                if (s.SessionStateType == SiteSessionTypes.FileSystem)
                {
                    DirectoryInfo di = new DirectoryInfo(s.TMPPath + Path.DirectorySeparatorChar + "Sessions");
                    if (di.Exists)
                    {
                        foreach (FileInfo fi in di.GetFiles("*.xml"))
                        {
                            if (SessionState.GetExpiryFromFile(fi.FullName).Ticks < DateTime.Now.Ticks)
                                fi.Delete();
                        }
                    }
                }
            }
            if (_ipSessionIds == null)
                _ipSessionIds = new Dictionary<string, CachedItemContainer>();
            string[] keys = new string[_ipSessionIds.Count];
            _ipSessionIds.Keys.CopyTo(keys,0);
            foreach (string str in keys)
            {
                if (DateTime.Now.Subtract(_ipSessionIds[str].LastAccess).TotalMinutes < IP_SESSION_ID_MINUTES)
                    _ipSessionIds.Remove(str);
            }
            GC.Collect();
            Monitor.Exit(_lock);
        }

        internal static string GenerateSessionID()
        {
            string ret="";
            while (ret.Length < SESSION_ID_LEN)
                ret += _ALLOWED_SESSION_ID_CHARS[_rand.RandomRange(0, _ALLOWED_SESSION_ID_CHARS.Length - 1)].ToString();
            return ret;
        }

        public static void LoadStateForConnection(HttpRequest request,Site site)
        {
            switch (site.SessionStateType)
            {
                case SiteSessionTypes.ThreadState:
                    if (request.Cookie.SessionID == null)
                    {
                        Monitor.Enter(_lock);
                        if (_ipSessionIds == null)
                            _ipSessionIds = new Dictionary<string, CachedItemContainer>();
                        if (_ipSessionIds.ContainsKey(request.Connection.Client.ToString()))
                            request.Cookie.SessionID = _ipSessionIds[request.Connection.Client.ToString()].Value.ToString();
                        Monitor.Exit(_lock);
                    }
                    if (request.Cookie.SessionID != null)
                    {
                        Monitor.Enter(_lock);
                        if (_sessions == null)
                            _sessions = new List<SessionState>();
                        for (int x = 0; x < _sessions.Count; x++)
                        {
                            SessionState session = _sessions[x];
                            if (session.ID == request.Cookie.SessionID)
                            {
                                session.Renew(site.SessionTimeoutMinutes);
                                request.SetSession(session);
                                _sessions.RemoveAt(x);
                                _sessions.Insert(x, session);
                                break;
                            }
                        }
                        Monitor.Exit(_lock);
                    }
                    if (request.Session==null)
                    {
                        Monitor.Enter(_lock);
                        while (true)
                        {
                            bool okay = true;
                            string id = GenerateSessionID();
                            if (_sessions == null)
                                _sessions = new List<SessionState>();
                            foreach (SessionState session in _sessions)
                            {
                                if (session.ID == id)
                                {
                                    okay = false;
                                    break;
                                }
                            }
                            if (okay)
                            {
                                if (_ipSessionIds == null)
                                    _ipSessionIds = new Dictionary<string, CachedItemContainer>();
                                _ipSessionIds.Add(request.Connection.Client.ToString(), new CachedItemContainer(id));
                                SessionState ss = new SessionState(id);
                                ss.Renew(site.SessionTimeoutMinutes);
                                _sessions.Add(ss);
                                request.SetSession(ss);
                                break;
                            }
                        }
                        Monitor.Exit(_lock);
                        request.ResponseCookie.SessionID = request.Session.ID;
                    }
                    break;
                case SiteSessionTypes.FileSystem:
                    if (request.Cookie.SessionID == null)
                    {
                        Monitor.Enter(_lock);
                        if (_ipSessionIds == null)
                            _ipSessionIds = new Dictionary<string, CachedItemContainer>();
                        if (_ipSessionIds.ContainsKey(request.Connection.Client.ToString()))
                            request.Cookie.SessionID = _ipSessionIds[request.Connection.Client.ToString()].Value.ToString();
                        Monitor.Exit(_lock);
                    }
                    if (request.Cookie.SessionID != null)
                    {
                        Monitor.Enter(_lock);
                        DirectoryInfo di = new DirectoryInfo(site.TMPPath + Path.DirectorySeparatorChar + "Sessions");
                        if (!di.Exists)
                            di.Create();
                        if (di.GetFiles(request.Cookie.SessionID + ".xml").Length > 0)
                        {
                            SessionState ss = new SessionState(request.Cookie.SessionID);
                            ss.LoadFromFile(di.FullName + Path.DirectorySeparatorChar + request.Cookie.SessionID + ".xml");
                            ss.Renew(site.SessionTimeoutMinutes);
                            ss.StoreToFile(di.FullName + Path.DirectorySeparatorChar + request.Cookie.SessionID + ".xml");
                            request.SetSession(ss);
                        }
                        Monitor.Exit(_lock);
                    }
                    if (request.Session == null)
                    {
                        Monitor.Enter(_lock);
                        DirectoryInfo di = new DirectoryInfo(site.TMPPath + Path.DirectorySeparatorChar + "Sessions");
                        if (!di.Exists)
                            di.Create();
                        while (true)
                        {
                            string id = GenerateSessionID();
                            if (_sessions == null)
                                _sessions = new List<SessionState>();
                            if (di.GetFiles(id + ".xml").Length == 0)
                            {
                                if (_ipSessionIds == null)
                                    _ipSessionIds = new Dictionary<string, CachedItemContainer>();
                                _ipSessionIds.Add(request.Connection.Client.ToString(), new CachedItemContainer(id));
                                SessionState ss = new SessionState(id);
                                ss.StoreToFile(di.FullName + Path.DirectorySeparatorChar + id + ".xml");
                                request.SetSession(ss);
                                break;
                            }
                        }
                        Monitor.Exit(_lock);
                        request.ResponseCookie.SessionID = request.Session.ID;
                    }
                    break;
            }
        }

        public static void StoreSessionForConnection(HttpRequest request, Site site)
        {
            switch (site.SessionStateType)
            {
                case SiteSessionTypes.ThreadState:
                    if (request.Session != null)
                    {
                        Monitor.Enter(_lock);
                        for (int x = 0; x < _sessions.Count; x++)
                        {
                            if (_sessions[x].ID == request.Session.ID)
                            {
                                _sessions.RemoveAt(x);
                                break;
                            }
                        }
                        request.Session.Renew(site.SessionTimeoutMinutes);
                        _sessions.Add(request.Session);
                        Monitor.Exit(_lock);
                        request.ResponseCookie.SessionID = request.Session.ID;
                    }
                    break;
                case SiteSessionTypes.FileSystem:
                    if (request.Session != null)
                    {
                        Monitor.Enter(_lock);
                        DirectoryInfo di = new DirectoryInfo(site.TMPPath + Path.DirectorySeparatorChar + "Sessions");
                        if (!di.Exists)
                            di.Create();
                        request.Session.Renew(site.SessionTimeoutMinutes);
                        request.Session.StoreToFile(di.FullName + Path.DirectorySeparatorChar + request.Session.ID + ".xml");
                        Monitor.Exit(_lock);
                        request.ResponseCookie.SessionID = request.Session.ID;
                    }
                    break;
            }
        }
    }
}
