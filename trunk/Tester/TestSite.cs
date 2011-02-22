using System;
using System.Collections.Generic;
using System.Text;
using Org.Reddragonit.EmbeddedWebServer.Interfaces;

namespace Tester
{
    public class TestSite : Site
    {
        public override int Port
        {
            get
            {
                return 8080;
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
    }
}
