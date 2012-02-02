using System;
using System.Collections.Generic;
using System.Text;
using Org.Reddragonit.EmbeddedWebServer.Interfaces;
using Org.Reddragonit.EmbeddedWebServer.Attributes;

namespace Tester.services
{
    class TestService : EmbeddedService
    {
        [WebMethod(false)]
        public string HelloWorld()
        {
            return "Hello";
        }

        [WebMethod(false)]
        public string HelloMyName(string myname)
        {
            return "Hello " + myname;
        }

    }
}
