using System;
using System.Collections.Generic;
using System.Text;
using Org.Reddragonit.EmbeddedWebServer.Interfaces;
using Org.Reddragonit.EmbeddedWebServer.Components;
using System.Threading;
using Org.Reddragonit.EmbeddedWebServer.Components.Message;

namespace Tester
{
    public class TimeoutProducer : IRequestHandler
    {
        #region IRequestHandler Members

        public bool IsReusable
        {
            get { return true; }
        }

        public bool CanProcessRequest(HttpRequest request, Site site)
        {
            return request.URL.AbsolutePath == "/timeout.html";
        }

        public void ProcessRequest(HttpRequest request, Site site)
        {
            Thread.Sleep(site.RequestTimeout * 2);
            request.ResponseWriter.WriteLine("<h1>If you can read this the timeout failed.</h1>");
        }

        public void Init()
        {
        }

        public void DeInit()
        {
        }

        public bool RequiresSessionForRequest(HttpRequest request, Site site)
        {
            return false;
        }

        #endregion
    }
}
