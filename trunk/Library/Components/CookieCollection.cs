using System;
using System.Collections.Generic;
using System.Text;

namespace Org.Reddragonit.EmbeddedWebServer.Components
{
    /*
     * This class is used to provide simple access to 
     * the http cookies both providfed by the client
     * and set by the http connection.
     */
    public class CookieCollection
    {
        //Default cookie expiration that can be overridden by a site.
        public const int DEFAULT_COOKIE_DURATION_MINUTES = 60;

        //houses the values that exist in the cookie.
        //these are either set in code, or loaded
        //from the request.
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

        //houses the expiration date to provide the browser for the given cookie values
        private DateTime _expiry;
        public DateTime Expiry
        {
            get { return _expiry; }
        }

        //return the list of names for the cookie values
        public List<string> Keys
        {
            get
            {
                string[] tmp = new string[_values.Count];
                _values.Keys.CopyTo(tmp, 0);
                return new List<string>(tmp);
            }
        }

        //called to renew the expiration by a specified number of minutes
        public void Renew(int minutes)
        {
            _expiry = DateTime.Now.AddMinutes(minutes);
        }

        //called to expire the given cookie values
        public void Expire()
        {
            _expiry = DateTime.Now.AddDays(-1);
        }

        //returns the session id as stored in the cookie, this is used to load the session information
        internal string SessionID
        {
            get { return this[Messages.Current["Org.Reddragonit.EmbeddedWebServer.Components.CookieCollections.SessionID"]]; }
            set { this[Messages.Current["Org.Reddragonit.EmbeddedWebServer.Components.CookieCollections.SessionID"]] = value; }
        }

        //constructor only exists internal since it is created by the http connection
        internal CookieCollection(){
            _values = new Dictionary<string, string>();
            Renew(DEFAULT_COOKIE_DURATION_MINUTES);
        }

        //this contructor parses the cookie values from the given connection as specified by
        //the client browser
        internal CookieCollection(string cookieString)
        {
            _values = new Dictionary<string, string>();
            if (cookieString != null)
            {
                foreach (string str in cookieString.Split(';'))
                {
                    if (str.Length > 0)
                    {
                        if (_values.ContainsKey(str.Trim().Split('=')[0]))
                            _values.Remove(str.Trim().Split('=')[0]);
                        _values.Add(str.Trim().Split('=')[0], str.Trim().Split('=')[1]);
                    }
                }
            }
            Renew(DEFAULT_COOKIE_DURATION_MINUTES);
        }

        //returns a cookie string equivalent
        public override string ToString()
        {
            string ret = "";
            foreach (string str in _values.Keys)
                ret += str + "=" + _values[str] + "; ";
            return ret;
        }
    }
}
