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
    }
}
