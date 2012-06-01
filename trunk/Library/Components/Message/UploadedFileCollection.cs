using System;
using System.Collections.Generic;
using System.Text;

namespace Org.Reddragonit.EmbeddedWebServer.Components.Message
{
    public class UploadedFileCollection
    {
        private Dictionary<string, UploadedFile> _values;
            public UploadedFile this[string name]
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

            internal UploadedFileCollection(Dictionary<string, UploadedFile> vals)
            {
                _values = vals;
            }
    }
}
