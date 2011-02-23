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
                if (value!=null)
                    _values.Add(name, value);
            }
        }

        public List<string> Keys
        {
            get
            {
                string[] tmp = new string[_values.Count];
                _values.Keys.CopyTo(tmp, 0);
                return new List<string>(tmp);
            }
        }

        public string Host
        {
            get { return this["Host"]; }
            set { this["Host"] = value; }
        }

        public string ContentLength
        {
            get { return this["Content-Length"]; }
            set { this["Content-Length"] = value; }
        }

        public string ContentType
        {
            get { return this["Content-Type"]; }
            set { this["Content-Type"] = value; }
        }

        public string Date
        {
            get { return this["Date"]; }
            set { this["Date"] = value; }
        }

        public HeaderCollection()
        {
            _values = new Dictionary<string, string>();
        }
    }
}
