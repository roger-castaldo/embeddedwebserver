using System;
using System.Collections.Generic;
using System.Text;
using Org.Reddragonit.EmbeddedWebServer.Components;
using System.Security.Cryptography;
using Org.Reddragonit.EmbeddedWebServer.BasicHandlers;
using System.Reflection;
using Procurios.Public;
using System.Collections;
using Org.Reddragonit.EmbeddedWebServer.Diagnostics;
using Org.Reddragonit.EmbeddedWebServer.Components.Message;

namespace Org.Reddragonit.EmbeddedWebServer.Interfaces
{
    /*
     * This is the abstract implementation of the embedded service class.
     * All embedded web services must inherit this class in order 
     * to be loaded as an embedded web service.  The only
     * function that can be overriden is the one to determine 
     * valid security access to a given function call.
     */
    public class EmbeddedService
    {
        //returns if calling the specified function is allowed
        protected virtual bool IsValidAccess(string functionName)
        {
            return true;
        }

        //houses the current http connection being used for the current request
        private HttpRequest _request;
        public HttpRequest Request
        {
            get { return _request; }
        }

        //houses the current site being used for the current request
        private Site _site;
        public Site WebSite
        {
            get { return _site; }
        }

        public EmbeddedService()
        {
        }

        //returns the base url to access the service
        public string URL
        {
            get
            {
                SHA256Managed hasher = new SHA256Managed();
                string[] split = this.GetType().FullName.Split('.');
                string path = "";
                if (split.Length > 2)
                {
                    byte[] tmp = new byte[(256 / 8) + 1];
                    hasher.ComputeHash(ASCIIEncoding.ASCII.GetBytes(this.GetType().FullName.Substring(0, this.GetType().FullName.Length - split[split.Length - 1].Length - 1 - split[split.Length - 2].Length - 1))).CopyTo(tmp, 0);
                    tmp[tmp.Length - 1] = 1;
                    path = Convert.ToBase64String(tmp).Replace("+", "_").Replace("/", "%23");
                    path += "/" + split[split.Length - 2] + "/" + split[split.Length - 1];
                }
                else if (split.Length == 2)
                    path = split[split.Length - 2] + "/" + split[split.Length - 1];
                else
                    path = split[0];
                path = "/Resources/services/" + path;
                path += "." + EmbeddedServiceHandler.EMBEDDED_SERVICE_EXTENSION;
                return path;
            }
        }

        internal const string CONTEXT_PARS_VARIABLE = "EmbeddedServiceParameters";
        internal const string CONTEXT_METHOD_VARIABLE = "EmbeddedServiceMethod";

        internal void GetMethodForRequest(HttpRequest request, Site website)
        {
            string functionName = request.URL.AbsolutePath.Substring(request.URL.AbsolutePath.LastIndexOf("/") + 1);
            MethodInfo mi = null;
            List<MethodInfo> methods = new List<MethodInfo>();
            foreach (MethodInfo m in GetType().GetMethods())
            {
                if (m.Name == functionName)
                    methods.Add(m);
            }
            if (methods.Count == 0)
            {
                throw new Exception(string.Format(Messages.Current["Org.Reddragonit.EmbeddedWebServer.Interfaces.EmbeddedService.Errors.UnableToLocateFunction"], functionName, GetType().FullName));
            }
            object val = request.JSONParameter;

            if (val == null)
            {
                foreach (MethodInfo m in methods)
                {
                    if (m.GetParameters().Length == 0)
                    {
                        mi = m;
                        break;
                    }
                }
                if (mi == null)
                {
                    throw new Exception(string.Format(Messages.Current["Org.Reddragonit.EmbeddedWebServer.Interfaces.EmbeddedService.Errors.UnableToLocateFunctionWithParameters"], new object[] { functionName, GetType().FullName, "none" }));
                }
                request[CONTEXT_PARS_VARIABLE] = new object[0];
            }
            else
            {
                List<string> pars = new List<string>();
                foreach (string str in ((Hashtable)val).Keys)
                {
                    pars.Add(str);
                }
                foreach (MethodInfo m in methods)
                {
                    bool containsAll = true;
                    foreach (string str in pars)
                    {
                        bool found = false;
                        foreach (ParameterInfo pi in m.GetParameters())
                        {
                            if (pi.Name == str)
                            {
                                found = true;
                                break;
                            }
                        }
                        if (!found)
                        {
                            containsAll = false;
                            break;
                        }
                    }
                    if (pars.Count != m.GetParameters().Length)
                        containsAll = false;
                    if (containsAll)
                    {
                        mi = m;
                        break;
                    }
                }
                if ((mi == null) && (methods.Count == 1))
                {
                    foreach (ParameterInfo pi in methods[0].GetParameters())
                    {
                        if (!pars.Contains(pi.Name))
                        {
                            pars.Add(pi.Name);
                            ((Hashtable)val).Add(pi.Name, null);
                        }
                    }
                    mi = methods[0];
                }
                if (mi == null)
                {
                    string epars = "";
                    foreach (string par in pars)
                        epars += par + ",";
                    epars = epars.Substring(0, epars.Length - 1);
                    throw new Exception(string.Format(Messages.Current["Org.Reddragonit.EmbeddedWebServer.Interfaces.EmbeddedService.Errors.UnableToLocateFunctionWithParameters"], new object[] { functionName, GetType().FullName, epars }));
                }
                object[] funcPars = new object[pars.Count];
                for (int x = 0; x < funcPars.Length; x++)
                {
                    ParameterInfo pi = mi.GetParameters()[x];
                    funcPars[x] = ConvertObjectToType(((Hashtable)val)[pi.Name], pi.ParameterType);
                }
                request[CONTEXT_PARS_VARIABLE] = funcPars;
            }
            request[CONTEXT_METHOD_VARIABLE] = mi;
        }

        /*
         * This function is the main portion of the class.  It parses out the parameters,
         * locates the apprporiate function and then proceeds to invoke it, all assuming
         * that the security check passes.
         */
        public void Invoke(HttpRequest request, Site website)
        {
            _request = request;
            _site = website;
            MethodInfo mi = (MethodInfo)request[CONTEXT_METHOD_VARIABLE];
            if (!IsValidAccess(mi.Name))
            {
                request.ResponseStatus = HttpStatusCodes.Forbidden;
                request.ResponseWriter.Write(string.Format(Messages.Current["Org.Reddragonit.EmbeddedWebServer.Interfaces.EmbeddedService.Errors.InvalidAccess"],this.GetType().FullName, mi.Name));
                return;
            }
            if (mi.ReturnType.Name == "void")
                mi.Invoke(this, (object[])request[CONTEXT_PARS_VARIABLE]);
            else
                request.ResponseWriter.Write(JSON.JsonEncode(mi.Invoke(this, (object[])request[CONTEXT_PARS_VARIABLE])));
        }

        //this function is used to convert a submitted value parameter to a given
        //object
        private object ConvertObjectToType(object obj, Type expectedType)
        {
            if (expectedType.Equals(typeof(bool)) && (obj == null))
                return false;
            if (obj == null)
                return null;
            if (obj.GetType().Equals(expectedType))
                return obj;
            if (expectedType.Equals(typeof(string)))
                return obj.ToString();
            if (expectedType.IsEnum)
                return Enum.Parse(expectedType, obj.ToString());
            try
            {
                object ret = Convert.ChangeType(obj, expectedType);
                return ret;
            }
            catch (Exception e)
            {
                Logger.LogError(e);
            }
            if (expectedType.IsArray || (obj is ArrayList))
            {
                int count = 1;
                Type underlyingType = null;
                if (expectedType.IsGenericType)
                    underlyingType = expectedType.GetGenericArguments()[0];
                else
                    underlyingType = expectedType.GetElementType();
                if (obj is ArrayList)
                    count = ((ArrayList)obj).Count;
                Array ret = Array.CreateInstance(underlyingType, count);
                ArrayList tmp = new ArrayList();
                if (!(obj is ArrayList))
                {
                    tmp.Add(ConvertObjectToType(obj, underlyingType));
                }
                else
                {
                    for (int x = 0; x < ret.Length; x++)
                    {
                        tmp.Add(ConvertObjectToType(((ArrayList)obj)[x], underlyingType));
                    }
                }
                tmp.CopyTo(ret);
                return ret;
            }
            else if (expectedType.FullName.StartsWith("System.Collections.Generic.Dictionary"))
            {
                object ret = expectedType.GetConstructor(Type.EmptyTypes).Invoke(new object[0]);
                Type keyType = expectedType.GetGenericArguments()[0];
                Type valType = expectedType.GetGenericArguments()[1];
                foreach (string str in ((Hashtable)obj).Keys)
                {
                    ((IDictionary)ret).Add(ConvertObjectToType(str, keyType), ConvertObjectToType(((Hashtable)obj)[str], valType));
                }
                return ret;
            }
            else if (expectedType.FullName.StartsWith("System.Nullable"))
            {
                Type underlyingType = null;
                if (expectedType.IsGenericType)
                    underlyingType = expectedType.GetGenericArguments()[0];
                else
                    underlyingType = expectedType.GetElementType();
                if (obj == null)
                    return null;
                return ConvertObjectToType(obj, underlyingType);
            }
            else
            {
                object ret = expectedType.GetConstructor(Type.EmptyTypes).Invoke(new object[0]);
                foreach (string str in ((Hashtable)obj).Keys)
                {
                    PropertyInfo pi = expectedType.GetProperty(str);
                    pi.SetValue(ret, ConvertObjectToType(((Hashtable)obj)[str], pi.PropertyType), new object[0]);
                }
                return ret;
            }
        }
    }
}
