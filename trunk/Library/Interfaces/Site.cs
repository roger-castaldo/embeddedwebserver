using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using Org.Reddragonit.EmbeddedWebServer.Components;

namespace Org.Reddragonit.EmbeddedWebServer.Interfaces
{
    public abstract class Site
    {

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

        public virtual void ProcessRequest(HttpConnection conn)
        {

        }

        public Site() { }
    }
}
