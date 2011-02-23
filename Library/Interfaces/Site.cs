using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using Org.Reddragonit.EmbeddedWebServer.Components;
using Org.Reddragonit.EmbeddedWebServer.BasicHandlers;

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

        public virtual SiteSessionTypes SessionStateType
        {
            get { return SiteSessionTypes.ThreadState; }
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

        public virtual void Start()
        {
        }

        public virtual void Stop()
        {
        }

        public void ProcessRequest(HttpConnection conn)
        {
            bool found = false;
            foreach (IRequestHandler handler in Handlers)
            {
                if (handler.CanProcessRequest(conn, this))
                {
                    found = true;
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

        public Site() { }
    }
}
