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

        public virtual SiteSessionTypes SessionStateType
        {
            get { return SiteSessionTypes.ThreadState; }
        }

        public virtual string TMPPath
        {
            get { return "/tmp"; }
        }

        public virtual void ProcessRequest(HttpConnection conn)
        {
            Console.WriteLine("Request Parameters: ");
            foreach (string str in conn.RequestParameters.Keys)
            {
                if (str == null)
                    Console.WriteLine("NULL: " + conn.RequestParameters[str]);
                else
                    Console.WriteLine(str + ": " + conn.RequestParameters[str]);
            }
            Console.WriteLine("Uploaded Files: ");
            foreach (string str in conn.UploadedFiles.Keys)
            {
                if (str == null)
                    Console.WriteLine("NULL: " + conn.UploadedFiles[str].FileName);
                else
                    Console.WriteLine(str + ": " + conn.UploadedFiles[str].FileName);
            }
        }

        public Site() { }
    }
}
