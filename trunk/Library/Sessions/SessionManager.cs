using System;
using System.Collections.Generic;
using System.Text;
using Org.Reddragonit.EmbeddedWebServer.Components;
using Org.Reddragonit.EmbeddedWebServer.Interfaces;
using System.Threading;
using System.IO;
using Org.Reddragonit.EmbeddedWebServer.Attributes;

namespace Org.Reddragonit.EmbeddedWebServer.Sessions
{
    internal class SessionManager : IBackgroundOperationContainer
    {
        private const string _ALLOWED_SESSION_ID_CHARS = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        private const int SESSION_ID_LEN = 12;
        private const int THREAD_SLEEP = 60000;

        private static object _lock = new object();
        private static List<SessionState> _sessions;
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
            GC.Collect();
            Monitor.Exit(_lock);
        }

        private static string GenerateSessionID()
        {
            string ret="";
            while (ret.Length < SESSION_ID_LEN)
                ret += _ALLOWED_SESSION_ID_CHARS[_rand.RandomRange(0, _ALLOWED_SESSION_ID_CHARS.Length - 1)].ToString();
            return ret;
        }

        public static void LoadStateForConnection(HttpConnection conn,Site site)
        {
            switch (site.SessionStateType)
            {
                case SiteSessionTypes.ThreadState:
                    if (conn.RequestCookie.SessionID != null)
                    {
                        Monitor.Enter(_lock);
                        if (_sessions == null)
                            _sessions = new List<SessionState>();
                        foreach (SessionState session in _sessions)
                        {
                            if (session.ID == conn.RequestCookie.SessionID)
                            {
                                conn.SetSession(session);
                                conn.Session.Renew(site.SessionTimeoutMinutes);
                                break;
                            }
                        }
                        Monitor.Exit(_lock);
                    }
                    if (conn.Session==null)
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
                                SessionState ss = new SessionState(id);
                                _sessions.Add(ss);
                                conn.SetSession(ss);
                                break;
                            }
                        }
                        Monitor.Exit(_lock);
                        conn.ResponseCookie.SessionID = conn.Session.ID;
                    }
                    break;
                case SiteSessionTypes.FileSystem:
                    if (conn.RequestCookie.SessionID != null)
                    {
                        Monitor.Enter(_lock);
                        DirectoryInfo di = new DirectoryInfo(site.TMPPath + Path.DirectorySeparatorChar + "Sessions");
                        if (!di.Exists)
                            di.Create();
                        if (di.GetFiles(conn.RequestCookie.SessionID + ".xml").Length > 0)
                        {
                            SessionState ss = new SessionState(conn.RequestCookie.SessionID);
                            ss.LoadFromFile(di.FullName + Path.DirectorySeparatorChar + conn.RequestCookie.SessionID + ".xml");
                            conn.SetSession(ss);
                        }
                        Monitor.Exit(_lock);
                    }
                    if (conn.Session == null)
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
                            if (di.GetFiles(id+".xml").Length==0)
                            {
                                SessionState ss = new SessionState(id);
                                ss.StoreToFile(di.FullName + Path.DirectorySeparatorChar + id + ".xml");
                                conn.SetSession(ss);
                                break;
                            }
                        }
                        Monitor.Exit(_lock);
                        conn.ResponseCookie.SessionID = conn.Session.ID;
                    }
                    break;
            }
        }

        public static void StoreSessionForConnection(HttpConnection conn, Site site)
        {
            switch (site.SessionStateType)
            {
                case SiteSessionTypes.ThreadState:
                    if (conn.Session != null)
                    {
                        Monitor.Enter(_lock);
                        for (int x = 0; x < _sessions.Count; x++)
                        {
                            if (_sessions[x].ID == conn.Session.ID)
                            {
                                _sessions.RemoveAt(x);
                                break;
                            }
                        }
                        _sessions.Add(conn.Session);
                        Monitor.Exit(_lock);
                        conn.ResponseCookie.SessionID = conn.Session.ID;
                    }
                    break;
                case SiteSessionTypes.FileSystem:
                    if (conn.Session != null)
                    {
                        Monitor.Enter(_lock);
                        DirectoryInfo di = new DirectoryInfo(site.TMPPath + Path.DirectorySeparatorChar + "Sessions");
                        if (!di.Exists)
                            di.Create();
                        conn.Session.StoreToFile(di.FullName + Path.DirectorySeparatorChar + conn.Session.ID + ".xml");
                        Monitor.Exit(_lock);
                        conn.ResponseCookie.SessionID = conn.Session.ID;
                    }
                    break;
            }
        }
    }
}
