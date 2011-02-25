using System;
using System.Collections.Generic;
using System.Text;
using Org.Reddragonit.EmbeddedWebServer.Interfaces;
using Org.Reddragonit.EmbeddedWebServer.Components;
using System.Reflection;
using Org.Reddragonit.EmbeddedWebServer.Attributes;
using System.Security.Cryptography;
using System.Threading;
using Org.Reddragonit.EmbeddedWebServer.Minifiers;

namespace Org.Reddragonit.EmbeddedWebServer.BasicHandlers
{
    /*
     * This handler is implemented to handle the embedded services that 
     * get defined in code.  It handles both supplying the javascript 
     * to access these services as well as the calls to the services.
     */
    public class EmbeddedServiceHandler : IRequestHandler,IBackgroundOperationContainer
    {
        //the extension to use for embedded resources
        public const string EMBEDDED_SERVICE_EXTENSION = "easmx";
        //the time to hold cached generated javascripts
        private const int CACHE_MINUTES = 60;

        //the lock used to access the cached javascript
        private object _lock = new object();
        //houses the cached generated javascript files
        private Dictionary<string, CachedItemContainer> _generatedJS;
        //houses the paths mapped to type string for the embedded services handle by this handler
        private Dictionary<string, string> _pathMaps;

        /*
         * This background threaded operation gets called every hour on the hour to clean out
         * all of the cached javascript that has not been accessed for 60 minutes.  It runs through 
         * all sites available and all instances of this handler
         */
        [BackgroundOperationCall(0, -1, -1, -1, BackgroundOperationDaysOfWeek.All)]
        public static void CleanCache()
        {
            List<Site> sites = ServerControl.Sites;
            foreach (Site site in sites)
            {
                foreach (IRequestHandler handler in site.Handlers)
                {
                    if (handler is EmbeddedServiceHandler)
                    {
                        EmbeddedServiceHandler esh = (EmbeddedServiceHandler)handler;
                        Monitor.Enter(esh._lock);
                        string[] keys = new string[esh._generatedJS.Count];
                        esh._generatedJS.Keys.CopyTo(keys, 0);
                        foreach (string str in keys)
                        {
                            if (DateTime.Now.Subtract(esh._generatedJS[str].LastAccess).TotalMinutes > CACHE_MINUTES)
                                esh._generatedJS.Remove(str);
                        }
                        Monitor.Exit(esh._lock);
                    }
                }
            }
            GC.Collect();
        }

        /*
         * This function is used to process a call made to an embedded service.
         * It loads the embedded service specified then invokes the request.
         */
        private void ProcessEmbeddedServiceCall(HttpConnection conn, Site site)
        {
            string type = conn.URL.AbsolutePath.Substring(0, conn.URL.AbsolutePath.LastIndexOf("/"));
            if (_pathMaps.ContainsKey(type))
            {
                conn.ResponseHeaders.ContentType="application/json";
                Type t = Utility.LocateType(_pathMaps[type]);
                EmbeddedService es = (EmbeddedService)t.GetConstructor(Type.EmptyTypes).Invoke(new object[0]);
                es.Invoke(conn, site);
            }
            else
                conn.ResponseStatus = HttpStatusCodes.Not_Found;
        }

        /*
         * This function is used to process a call made to get the javascript 
         * to access a given service.  It appends code to add jquery or json 
         * libraries if necessary.  It scans through the type specified 
         * finding all methods that are tagged as web methods and generates
         * code to handle each method.
         */
        private void ProcessJSGeneration(HttpConnection conn, Site site)
        {
            Type t = Utility.LocateType(conn.RequestParameters["TYPE"]);
            if (t == null)
            {
                conn.ResponseStatus = HttpStatusCodes.Not_Found;
            }
            else
            {
                conn.ResponseHeaders.ContentType = "text/javascript";
                bool create = true;
                Monitor.Enter(_lock);
                if (_generatedJS.ContainsKey(t.FullName))
                {
                    conn.ResponseWriter.Write(_generatedJS[t.FullName].Value);
                    create = false;
                }
                Monitor.Exit(_lock);
                if (create)
                {
                    string path = GetPathForType(t);

                    StringBuilder sw = new StringBuilder();
                    if (site.AddJqueryJavascript)
                    {
                        sw.AppendLine("if (document.getElementsByName(\"jqueryScriptTag\").length==0){");
                        sw.AppendLine("var e=window.document.createElement('script');");
                        sw.AppendLine("e.setAttribute('src','/jquery.js');");
                        sw.AppendLine("document.getElementsByTagName('head')[0].insertBefore(e,document.getElementsByTagName('head')[0].childNodes[0]);}");
                    }
                    if (site.AddJsonJavascript)
                    {
                        sw.AppendLine("if (document.getElementsByName(\"jsonScriptTag\").length==0){");
                        sw.AppendLine("var e=window.document.createElement('script');");
                        sw.AppendLine("e.setAttribute('src','/json.js');");
                        sw.AppendLine("document.getElementsByTagName('head')[0].insertBefore(e,document.getElementsByTagName('head')[0].childNodes[0]);}");
                    }
                    sw.AppendLine("var splitted = '" + t.FullName + "'.split('.');" +
                    "var tmp = splitted[0]; " +
                    "if (window[tmp]==undefined){ " +
                    "    window[tmp]= new Object(); " +
                    "} " +
                    "var curObj = window[tmp]; " +
                    "for (var x=1;x<splitted.length;x++){ " +
                    "    if (curObj[splitted[x]]==undefined){ " +
                    "        curObj[splitted[x]]={}; " +
                    "    } " +
                    "    tmp+='.'+splitted[x]; " +
                    "    curObj=curObj[splitted[x]]; " +
                    "}");
                    sw.Append(t.FullName + " = {fullNameSpace:'" + t.Namespace + "'");
                    foreach (MethodInfo mi in t.GetMethods())
                    {
                        if (mi.GetCustomAttributes(typeof(WebMethod), true).Length > 0)
                        {
                            sw.AppendLine(",");
                            GenerateFunctionCall(mi, t, path, sw);
                        }
                    }
                    sw.AppendLine("};\n");
                    string res = JSMinifier.Minify(sw.ToString());
                    Monitor.Enter(_lock);
                    if (!_generatedJS.ContainsKey(t.FullName))
                        _generatedJS.Add(t.FullName, new CachedItemContainer(res));
                    Monitor.Exit(_lock);
                    conn.ResponseWriter.Write(res);
                }
            }
        }

        //creates a hashed path to access a given embedded service type
        private string GetPathForType(Type t)
        {
            SHA256Managed hasher = new SHA256Managed();
            string[] split = t.FullName.Split('.');
            string path = "";
            if (split.Length > 2)
            {
                byte[] tmp = new byte[(256 / 8) + 1];
                hasher.ComputeHash(System.Text.ASCIIEncoding.ASCII.GetBytes(t.FullName.Substring(0, t.FullName.Length - split[split.Length - 1].Length - 1 - split[split.Length - 2].Length - 1))).CopyTo(tmp, 0);
                tmp[tmp.Length - 1] = 1;
                path = Convert.ToBase64String(tmp).Replace("+", "_").Replace("/", "%23");
                path += "/" + split[split.Length - 2] + "/" + split[split.Length - 1];
            }
            else if (split.Length == 2)
                path = split[split.Length - 2] + "/" + split[split.Length - 1];
            else
                path = split[0];
            path = "/Resources/services/" + path;
            path += "." + EMBEDDED_SERVICE_EXTENSION;
            return path;
        }

        //called to generate the javscript to call a given embedded service method.
        private void GenerateFunctionCall(MethodInfo mi, Type t, string url, StringBuilder sw)
        {
            sw.Append(mi.Name + " : function(");
            foreach (ParameterInfo pi in mi.GetParameters())
            {
                sw.Append(pi.Name + ",");
            }
            sw.AppendLine("OnSuccess,OnError,additionalVariables,synchronous,usePost){\n");
            sw.Append("\tvar function_data = JSON.stringify({");
            string vars = "";
            foreach (ParameterInfo pi in mi.GetParameters())
            {
                vars += "'" + pi.Name + "' : " + pi.Name + ",";
            }
            if (vars.Length > 0)
                vars = vars.Substring(0, vars.Length - 1);
            sw.Append(vars);
            sw.AppendLine("});");
            sw.AppendLine("\t$.ajax({\n" +
                         "\t\ttype:((usePost == undefined || !usePost) ? \"GET\" : \"POST\"),\n" +
                         "\t\turl: \"" + url + "/" + mi.Name + "\",\n" +
                         "\t\tdata: function_data,\n" +
                         "\t\tcontentType: \"application/json; charset=utf-8\",\n" +
                         "\t\tdataType: \"json\",\n" +
                         "\t\tsuccess: OnSuccess,\n" +
                         "\t\terror: OnError,\n" +
                         "\t\tadditionalVariables: additionalVariables\n," +
                         "\t\tasync:((synchronous == undefined) ? true : !synchronous)});");
            sw.AppendLine("}\n");
        }

        public EmbeddedServiceHandler()
        {
            _generatedJS = new Dictionary<string, CachedItemContainer>();
        }

        #region IRequestHandler Members

        //nothing is request specific so it is reusable
        public bool IsReusable
        {
            get { return true; }
        }

        //returns true if the requested url is for an embedded service or to generate javascript
        public bool CanProcessRequest(HttpConnection conn, Site site)
        {
            return _pathMaps.ContainsKey(conn.URL.AbsolutePath.Substring(0,conn.URL.AbsolutePath.LastIndexOf("/")))||(conn.URL.AbsolutePath.EndsWith("EmbeddedJSGenerator.js") && (conn.RequestParameters["TYPE"] != null));
        }

        //processes the reuqest as to whether or not it should generate javascript 
        //or perform the given operation
        public void ProcessRequest(HttpConnection conn, Site site)
        {
            if (conn.URL.AbsolutePath.EndsWith("EmbeddedJSGenerator.js"))
                ProcessJSGeneration(conn, site);
            else
                ProcessEmbeddedServiceCall(conn, site);
        }

        //initializes the embedded service paths for the given site and 
        //places them into its path maps list.
        public void Init()
        {
            _pathMaps = new Dictionary<string, string>();
            foreach (Type t in Site.CurrentSite.EmbeddedServiceTypes)
            {
                _pathMaps.Add(GetPathForType(t), t.FullName);
            }
        }

        public void DeInit()
        {
        }

        /*
         * searches through all methods of the requested name, without
         * checking parameters as that will delay things and they cannot get passed back.
         * If any method with that name requires the session state, then return true to create
         * a session within the request.
        */
        public bool RequiresSessionForRequest(HttpConnection conn, Site site)
        {
            string type = conn.URL.AbsolutePath.Substring(0, conn.URL.AbsolutePath.LastIndexOf("/"));
            if (_pathMaps.ContainsKey(type))
            {
                Type t = Utility.LocateType(_pathMaps[type]);
                EmbeddedService es = (EmbeddedService)t.GetConstructor(Type.EmptyTypes).Invoke(new object[0]);
                es.GetMethodForRequest(conn, site);
                if (conn[EmbeddedService.CONTEXT_METHOD_VARIABLE] != null)
                {
                    return ((WebMethod)((MethodInfo)conn[EmbeddedService.CONTEXT_METHOD_VARIABLE]).GetCustomAttributes(typeof(WebMethod), true)[0]).UseSession;
                }
            }
            return false;
        }

        #endregion
    }
}
