using System;
using System.Collections.Generic;
using System.Text;

namespace Org.Reddragonit.EmbeddedWebServer.Attributes
{
    [AttributeUsage(AttributeTargets.Class,AllowMultiple=true)]
    public class EmbeddedServiceNamespace : Attribute
    {
        private string _namespace;
        public string NameSpace
        {
            get { return _namespace; }
        }

        public EmbeddedServiceNamespace(string Namespace)
        {
            _namespace = (Namespace.EndsWith(".") ? NameSpace.Trim('.') : NameSpace);
        }
    }
}
