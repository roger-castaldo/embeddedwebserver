﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Org.Reddragonit.EmbeddedWebServer.Components.Message
{
    /*
     * Used to easily access the http headers either supplied to the client
     * or that were supplied by the client.
     */
    public class HeaderCollection
    {
        //holds the headers as name value pairs
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
                {
                    if (name == "Content-Type" && value.Contains("charset="))
                    {
                        _charset = value.Substring(value.IndexOf("charset=") + "charset=".Length).Trim();
                        value = value.Substring(0, value.IndexOf(";"));
                    }
                    _values.Add(name, value);
                }
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

        //sets/gets the host value in the headers
        public string Host
        {
            get { return this["Host"]; }
            set { this["Host"] = value; }
        }

        //sets/gets the Content length value in the headers
        public string ContentLength
        {
            get { return this["Content-Length"]; }
            set { this["Content-Length"] = value; }
        }

        //sets/gets the Content Type value in the headers
        public string ContentType
        {
            get { return this["Content-Type"]; }
            set { this["Content-Type"] = value; }
        }

        //sets/gets the Content Type Boundary Value in the headers
        internal string ContentTypeBoundary
        {
            get { return this["Content-Type:Boundary"]; }
            set { this["Content-Type:Boundary"] = value; }
        }

        private string _charset=null;
        public string CharSet
        {
            get { return _charset; }
            set { _charset = value; }
        }

        //sets/gets the Date value in the headers
        public string Date
        {
            get { return this["Date"]; }
            set { this["Date"] = value; }
        }

        public string UserAgent
        {
            get { return this["User-Agent"]; }
        }

        private Browser _browser;
        public Browser Browser
        {
            get
            {
                if (UserAgent == null)
                    return null;
                if (_browser == null)
                    _browser = new Browser(UserAgent);
                return _browser;
            }
        }

        public HeaderCollection()
        {
            _values = new Dictionary<string, string>();
        }
    }
}
