using System;
using System.Collections.Generic;
using System.Text;

namespace Org.Reddragonit.EmbeddedWebServer.Components
{
    /*
  * Used to easily access the http parameters either supplied to the client
  * or that were supplied by the client.
  */
    public class ParameterCollection
    {
            //holds the parameters as name value pairs
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
                    if (value != null)
                        _values.Add(name, value);
                }
            }

            //returns the names of all available headers
            public List<string> Keys
            {
                get
                {
                    string[] tmp = new string[_values.Count];
                    _values.Keys.CopyTo(tmp, 0);
                    return new List<string>(tmp);
                }
            }

            public int Count
            {
                get { return _values.Count; }
            }

            internal ParameterCollection(Dictionary<string,string> vals)
            {
                _values = vals;
            }
        }
}
