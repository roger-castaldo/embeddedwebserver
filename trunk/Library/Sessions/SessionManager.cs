using System;
using System.Collections.Generic;
using System.Text;
using Org.Reddragonit.EmbeddedWebServer.Components;
using Org.Reddragonit.EmbeddedWebServer.Interfaces;

namespace Org.Reddragonit.EmbeddedWebServer.Sessions
{
    internal class SessionManager
    {
        private const string _ALLOWED_SESSION_ID_CHARS = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        private static object _lock = new object();
        private static List<SessionState> _sessions;

        public static void LoadStateForConnection(HttpConnection conn,Site site)
        {
            switch (site.SessionStateType)
            {
                case SiteSessionTypes.Cookie:
                    break;
            }
        }

        public static void StoreSessionForConnection(HttpConnection conn, Site site)
        {
            switch (site.SessionStateType)
            {
                case SiteSessionTypes.Cookie:
                    break;
            }
        }
    }
}
