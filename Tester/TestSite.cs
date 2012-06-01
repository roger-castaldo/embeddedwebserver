using System;
using System.Collections.Generic;
using System.Text;
using Org.Reddragonit.EmbeddedWebServer.Interfaces;
using Org.Reddragonit.EmbeddedWebServer.Components;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.IO;
using Org.Reddragonit.EmbeddedWebServer.Components.Message;

namespace Tester
{
    public class TestSite : Site
    {

        protected override void PostStart()
        {
            DeployPath("/src/", new PhysicalDirectoryFolder(new DirectoryInfo("C:\\var"),null));
        }

        public override Org.Reddragonit.EmbeddedWebServer.Diagnostics.DiagnosticsLevels DiagnosticsLevel
        {
            get
            {
                return Org.Reddragonit.EmbeddedWebServer.Diagnostics.DiagnosticsLevels.TRACE;
            }
        }

        public override Org.Reddragonit.EmbeddedWebServer.Diagnostics.DiagnosticsOutputs DiagnosticsOutput
        {
            get
            {
                return Org.Reddragonit.EmbeddedWebServer.Diagnostics.DiagnosticsOutputs.CONSOLE;
            }
        }

        public override sIPPortPair[] ListenOn
        {
            get
            {
                return new sIPPortPair[] { new sIPPortPair(IPAddress.Any, 8080,false),
                    new sIPPortPair(IPAddress.Loopback,8081,true)
                };
            }
        }

        public override X509Certificate GetCertificateForEndpoint(sIPPortPair pair)
        {
            return new X509Certificate(".\\testCert.cer");
        }

        public override SiteSessionTypes SessionStateType
        {
            get
            {
                return SiteSessionTypes.ThreadState;
            }
        }

        public override Dictionary<string, sEmbeddedFile> EmbeddedFiles
        {
            get
            {
                Dictionary<string, sEmbeddedFile> ret = new Dictionary<string, sEmbeddedFile>();
                ret.Add("/resources/images/accept.png", new sEmbeddedFile("Tester.resources.accept.png", "/resources/images/accept.png", EmbeddedFileTypes.Image, ImageTypes.png));
                ret.Add("/index.html", new sEmbeddedFile("Tester.TestPostPage.html", "/index.html", EmbeddedFileTypes.Text, null));
                return ret;
            }
        }

        public override List<IRequestHandler> Handlers
        {
            get
            {
                List<IRequestHandler> hlds = base.Handlers;
                hlds.Add(new TimeoutProducer());
                return hlds;
            }
        }

        protected override void PreRequest(HttpRequest request)
        {
            if (request.JSONParameter != null)
            {
                Console.WriteLine(request.JSONParameter.GetType().FullName);
            }
            else if (request.Parameters != null)
            {
                foreach (string str in request.Parameters.Keys)
                    Console.WriteLine("Parameter[" + str + "] = " + request.Parameters[str]);
            }
        }

        public override int RequestTimeout
        {
            get
            {
                return 1000;
            }
        }
    }
}
