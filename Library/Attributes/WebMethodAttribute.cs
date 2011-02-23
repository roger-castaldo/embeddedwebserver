using System;
using System.Collections.Generic;
using System.Text;

namespace Org.Reddragonit.EmbeddedWebServer.Attributes
{
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
