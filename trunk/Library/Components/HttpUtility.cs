using System;
using System.Collections.Generic;
using System.Text;
using System.Collections.Specialized;

namespace Org.Reddragonit.EmbeddedWebServer.Components
{
    public class HttpUtility
    {
        public static string UrlDecode(string url)
        {
            return Uri.UnescapeDataString(url);
        }

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
