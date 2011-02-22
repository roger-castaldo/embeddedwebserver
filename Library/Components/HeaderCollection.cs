using System;
using System.Collections.Generic;
using System.Text;

namespace Org.Reddragonit.EmbeddedWebServer.Components
{
    public class HeaderCollection
    {
        private Dictionary<string, string> _values;
        public string this[string name]
        {
            get
            {
                if (_values.ContainsKey(name))
                    return _values[name];
                return null;
            }
            set
            {
                if (_values.ContainsKey(name))
                    _values.Remove(name);
                _values.Add(name, value);
            }
        }

        public string Host
        {
            get { return this["Host"]; }
        }

        public string ContentLength
        {
            get { return this["Content-Length"]; }
        }

        public string ContentType
        {
            get { return this["Content-Type"]; }
        }

        public HeaderCollection()
        {
            _values = new Dictionary<string, string>();
        }
    }
}
