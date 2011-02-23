using System;
using System.Collections.Generic;
using System.Text;
using Org.Reddragonit.EmbeddedWebServer.Components;

namespace Org.Reddragonit.EmbeddedWebServer.Interfaces
{
    public interface IRequestHandler
    {
        bool IsReusable { get; }

        bool CanProcessRequest(HttpConnection conn, Site site);
        void ProcessRequest(HttpConnection conn,Site site);
        void Init();
        void DeInit();
    }
}
