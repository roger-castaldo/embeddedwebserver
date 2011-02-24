using System;
using System.Collections.Generic;
using System.Text;
using Org.Reddragonit.EmbeddedWebServer.Interfaces;
using Org.Reddragonit.EmbeddedWebServer.Components;

namespace Tester
{
    public class TestSite : Site
    {

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

        public override int Port
        {
            get
            {
                return 8080;
            }
        }

        public override SiteSessionTypes SessionStateType
        {
            get
            {
                return SiteSessionTypes.ThreadState;
            }
        }

        public override List<sEmbeddedFile> EmbeddedFiles
        {
            get
            {
                return new List<sEmbeddedFile>(
                    new sEmbeddedFile[]{
                        new sEmbeddedFile("Tester.resources.accept.png","/resources/images/accept.png",EmbeddedFileTypes.Image,ImageTypes.png),
                        new sEmbeddedFile("Tester.TestPostPage.html","/index.html",EmbeddedFileTypes.Text,null)
                    }
                    );
            }
        }

        protected override void _Start()
        {
        }

        protected override void _Stop()
        {
        }
    }
}
