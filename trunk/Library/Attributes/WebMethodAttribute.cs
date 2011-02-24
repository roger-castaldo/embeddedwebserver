using System;
using System.Collections.Generic;
using System.Text;

namespace Org.Reddragonit.EmbeddedWebServer.Attributes
{
    /*
     * Used to tag methods in an embedded service that should be exposed
     * as methods available in javascript.  The flag useSession will indicate
     * if loading the session is required when the request is being processed.
     */
    [AttributeUsage(AttributeTargets.Method)]
    public class WebMethodAttribute : Attribute
    {
        private bool _useSession;
        public bool UseSession
        {
            get { return _useSession; }
        }

        public WebMethodAttribute(bool useSession)
        {
            _useSession = useSession;
        }

        public WebMethodAttribute() : this(false)
        {
        }
    }
}
