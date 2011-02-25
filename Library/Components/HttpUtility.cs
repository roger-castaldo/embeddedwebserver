using System;
using System.Collections.Generic;
using System.Text;
using System.Collections.Specialized;

namespace Org.Reddragonit.EmbeddedWebServer.Components
{
    /*
     * This class is a stripped down implementation of the 
     * HttpUtility class found in the System.Web dll.  This
     * was done in order to strip more memory from the overall library when
     * loaded into ram and allow access only to required functions.
     */
    public class HttpUtility
    {
        //decodes a url encoded value
        public static string UrlDecode(string url)
        {
            return Uri.UnescapeDataString(url);
        }

        //parses a query string and returns the name value pairs
        public static NameValueCollection ParseQueryString(string query)
        {
            NameValueCollection ret = new NameValueCollection();
            if (query.StartsWith("?"))
                query = query.Substring(1);
            if (query.Length > 1)
            {
                foreach (string str in query.Split('&'))
                {
                    ret.Add(str.Substring(0, str.IndexOf("=")), str.Substring(str.IndexOf("=") + 1));
                }
            }
            return ret;
        }
    }
}
