using System;
using System.Collections.Generic;
using System.Text;
using Org.Reddragonit.EmbeddedWebServer.Interfaces;
using Org.Reddragonit.EmbeddedWebServer.Components;
using System.Threading;

namespace Tester
{
    public class TimeoutProducer : IRequestHandler
    {
        #region IRequestHandler Members

        public bool IsReusable
        {
            get { return true; }
        }

        public bool CanProcessRequest(HttpConnection conn, Site site)
        {
            return conn.URL.AbsolutePath == "/timeout.html";
        }

        public void ProcessRequest(HttpConnection conn, Site site)
        {
            Thread.Sleep(site.RequestTimeout * 2);
            conn.ResponseWriter.WriteLine("<h1>If you can read this the timeout failed.</h1>");
        }

        public void Init()
        {
        }

        public void DeInit()
        {
        }

        public bool RequiresSessionForRequest(HttpConnection conn, Site site)
        {
            return false;
        }

        #endregion
    }
}
