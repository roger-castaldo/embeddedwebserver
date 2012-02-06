using System;
using System.Collections.Generic;
using System.Text;
using Org.Reddragonit.EmbeddedWebServer.Interfaces;

namespace Org.Reddragonit.EmbeddedWebServer.Components
{
    public class BoundMultipleSSLException : Exception
    {
        public BoundMultipleSSLException(sIPPortPair pair) :
            base("An attempt was made to bind multiple sites to "+pair.Address.ToString()+":"+pair.Port.ToString()+" and request using ssl, unable to handle.")
        {
        }
    }
}
