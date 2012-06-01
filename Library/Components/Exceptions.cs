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

    public class ThreadTimeoutException : Exception
    {
        public ThreadTimeoutException(long timeout) :
            base("A thread has timed out after " + timeout.ToString() + "ms")
        {
        }
    }

    public class HttpException : Exception
    {
        private HttpStatusCodes _code;
        public HttpStatusCodes Code
        {
            get { return _code; }
        }

        public HttpException(HttpStatusCodes code, string errMsg) : base(errMsg) {
            _code = code;
        }
        public HttpException(HttpStatusCodes code, string errMsg, Exception inner)
            : base(errMsg, inner)
        {
            _code = code;
        }
    }

    public class BadRequestException : HttpException
    {
        public BadRequestException(string errMsg) : base(HttpStatusCodes.Bad_Request, errMsg) { }
        public BadRequestException(string errMsg,Exception inner) : base(HttpStatusCodes.Bad_Request, errMsg,inner) { }
    }

    public class ParserException : BadRequestException
    {
        public ParserException(string errMsg) : base(errMsg) { }
        public ParserException(string errMsg, Exception inner) : base(errMsg, inner) { }
    }
}
