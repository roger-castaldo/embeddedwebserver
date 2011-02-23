using System;
using System.Collections.Generic;
using System.Text;

namespace Org.Reddragonit.EmbeddedWebServer.Sessions
{
    public class SessionState
    {
        private string _id;
        public string ID
        {
            get { return _id; }
        }

        private DateTime _lastAccess;
        public DateTime LastAccess
        {
            get { return _lastAccess; }
            set { _lastAccess = value; }
        }

        private Dictionary<string, object> _content;

        public object this[string name]
        {
            get
            {
                if (_content.ContainsKey(name))
                    return _content[name];
                return null;
            }
            set
            {
                if (_content.ContainsKey(name))
                    _content.Remove(name);
                if (value != null)
                    _content.Add(name, value);
            }
        }

        public SessionState(string id)
        {
            _id = id;
            _lastAccess = new DateTime();
            _content = new Dictionary<string, object>();
        }
    }
}
