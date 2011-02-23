using System;
using System.Collections.Generic;
using System.Text;

namespace Org.Reddragonit.EmbeddedWebServer.Components
{
    public class CookieCollection
    {
        public const int DEFAULT_COOKIE_DURATION_MINUTES = 60;

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

        private DateTime _expiry;
        public DateTime Expiry
        {
            get { return _expiry; }
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

        public void Renew(int minutes)
        {
            _expiry = DateTime.Now.AddMinutes(minutes);
        }

        public void Expire()
        {
            _expiry = DateTime.Now.AddDays(-1);
        }

        internal string SessionID
        {
            get { return this[Messages.Current["Org.Reddragonit.EmbeddedWebServer.Components.CookieCollections.SessionID"]]; }
            set { this[Messages.Current["Org.Reddragonit.EmbeddedWebServer.Components.CookieCollections.SessionID"]] = value; }
        }

        internal CookieCollection(){
            _values = new Dictionary<string, string>();
            Renew(DEFAULT_COOKIE_DURATION_MINUTES);
        }

        internal CookieCollection(string cookieString)
        {
            _values = new Dictionary<string, string>();
            if (cookieString != null)
            {
                foreach (string str in cookieString.Split(';'))
                {
                    if (str.Length > 0)
                    {
                        _values.Add(str.Trim().Split('=')[0], str.Trim().Split('=')[1]);
                    }
                }
            }
            Renew(DEFAULT_COOKIE_DURATION_MINUTES);
        }

        public override string ToString()
        {
            string ret = "";
            foreach (string str in _values.Keys)
                ret += str + "=" + _values[str] + "; ";
            return ret;
        }
    }
}
