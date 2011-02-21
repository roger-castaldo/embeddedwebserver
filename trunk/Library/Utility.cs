using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.IO;
using System.Threading;
using System.Xml.Serialization;

namespace Org.Reddragonit.EmbeddedWebServer
{
    public class Utility
    {
        private static string basePath = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile.Substring(0, AppDomain.CurrentDomain.SetupInformation.ConfigurationFile.LastIndexOf(Path.DirectorySeparatorChar));

        public static DirectoryInfo LocateDirectory(string name)
        {
            if (name == "/")
                return new DirectoryInfo(basePath);
            else
                return recurLocateDirectory(name, new DirectoryInfo(basePath));
        }

        private static DirectoryInfo recurLocateDirectory(string name, DirectoryInfo curDirectory)
        {
            if (curDirectory.Name.ToUpper() == name.ToUpper())
                return curDirectory;
            else
            {
                foreach (DirectoryInfo di in curDirectory.GetDirectories())
                {
                    DirectoryInfo ret = recurLocateDirectory(name, di);
                    if (ret != null)
                        return ret;
                }
            }
            return null;
        }

        public static FileInfo LocateFile(string name)
        {
            return recurLocateFile(name, new DirectoryInfo(basePath));
        }

        private static FileInfo recurLocateFile(string name, DirectoryInfo directory)
        {
            foreach (FileInfo fi in directory.GetFiles())
            {
                if (fi.Name.ToUpper() == name.ToUpper())
                    return fi;
            }
            foreach (DirectoryInfo di in directory.GetDirectories())
            {
                FileInfo fi = recurLocateFile(name, di);
                if (fi != null)
                    return fi;
            }
            return null;
        }

        public static Type LocateType(string typeName)
        {
            Type t = Type.GetType(typeName, false, true);
            if (t == null)
            {
                foreach (Assembly ass in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        if (!ass.GetName().Name.Contains("mscorlib") && !ass.GetName().Name.StartsWith("System") && !ass.GetName().Name.StartsWith("Microsoft"))
                        {
                            t = ass.GetType(typeName, false, true);
                            if (t != null)
                                break;
                        }
                    }
                    catch (Exception e)
                    {
                        if (e.Message != "The invoked member is not supported in a dynamic assembly.")
                        {
                            throw e;
                        }
                    }
                }
            }
            return t;
        }

        public static List<Type> LocateTypeInstances(Type parent)
        {
            List<Type> ret = new List<Type>();
            foreach (Assembly ass in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (!ass.GetName().Name.Contains("mscorlib") && !ass.GetName().Name.StartsWith("System") && !ass.GetName().Name.StartsWith("Microsoft"))
                    {
                        foreach (Type t in ass.GetTypes())
                        {
                            if (t.IsSubclassOf(parent) || (parent.IsInterface && new List<Type>(t.GetInterfaces()).Contains(parent)))
                                ret.Add(t);
                        }
                    }
                }
                catch (Exception e)
                {
                    if (e.Message != "The invoked member is not supported in a dynamic assembly.")
                    {
                        throw e;
                    }
                }
            }
            return ret;
        }

        public static List<Type> LocateTypeInstances(Type parent, Assembly ass)
        {
            List<Type> ret = new List<Type>();
            try
            {
                foreach (Type t in ass.GetTypes())
                {
                    if (t.IsSubclassOf(parent) || (parent.IsInterface && new List<Type>(t.GetInterfaces()).Contains(parent)))
                        ret.Add(t);
                }
            }
            catch (Exception e)
            {
                if (e.Message != "The invoked member is not supported in a dynamic assembly.")
                {
                    throw e;
                }
            }
            return ret;
        }

        public static Stream LocateEmbededResource(string name)
        {
            Stream ret = typeof(Utility).Assembly.GetManifestResourceStream(name);
            if (ret == null)
            {
                foreach (Assembly ass in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        if (!ass.GetName().Name.Contains("mscorlib") && !ass.GetName().Name.StartsWith("System") && !ass.GetName().Name.StartsWith("Microsoft"))
                        {
                            ret = ass.GetManifestResourceStream(name);
                            if (ret != null)
                                break;
                        }
                    }
                    catch (Exception e)
                    {
                        if (e.Message != "The invoked member is not supported in a dynamic assembly.")
                        {
                            throw e;
                        }
                    }
                }
            }
            return ret;
        }

        public static string ReadEmbeddedResource(string name)
        {
            Stream s = LocateEmbededResource(name);
            string ret = "";
            if (s != null)
            {
                TextReader tr = new StreamReader(s);
                ret = tr.ReadToEnd();
                tr.Close();
            }
            return ret;
        }

        public static Dictionary<string,string> ParseProperties(string propertiesText)
        {
            Dictionary<string, string> ret = new Dictionary<string, string>();
            foreach (string str in propertiesText.Split('\n'))
            {
                if ((str.Length > 0) && !str.StartsWith("#") && str.Contains(":"))
                {
                    string name = str.Substring(0, str.IndexOf(":"));
                    string value = str.Substring(str.IndexOf(":") + 1);
                    if (value.Contains("#"))
                    {
                        for (int x = 0; x < value.Length; x++)
                        {
                            if (value[x] == '#')
                            {
                                if ((x > 0) && (value[x - 1] != '\\'))
                                {
                                    value = value.Substring(0, x);
                                    break;
                                }
                                else if (x == 0)
                                    value = "";
                            }
                        }
                    }
                    if (value.Length > 0)
                    {
                        if (ret.ContainsKey(name))
                        {
                            ret.Remove(name);
                        }
                        ret.Add(name, value.Trim());
                    }
                }
            }
            return ret;
        }

        public static bool StringsEqual(string str1, string str2)
        {
            if ((str1 == null) && (str2 != null))
                return false;
            else if ((str1 != null) && (str2 == null))
                return false;
            else if ((str1 == null) && (str2 == null))
                return true;
            else
                return str1.Equals(str2);
        }

        public static string ConvertObjectToXML(object obj)
        {
            if (obj == null)
                return null;
            MemoryStream ms = new MemoryStream();
            XmlSerializer.FromTypes(new Type[] { obj.GetType() })[0].Serialize(ms, obj);
            return ASCIIEncoding.ASCII.GetString(ms.ToArray());
        }

        public static object ConvertObjectFromXML(Type type, string xmlCode)
        {
            if (xmlCode == null)
                return null;
            return XmlSerializer.FromTypes(new Type[] { type })[0].Deserialize(new MemoryStream(ASCIIEncoding.ASCII.GetBytes(xmlCode)));
        }

        static Utility(){
        }
    }
}
