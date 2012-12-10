using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.IO;
using System.Threading;
using System.Xml.Serialization;
using Org.Reddragonit.EmbeddedWebServer.Diagnostics;
using Org.Reddragonit.EmbeddedWebServer.Interfaces;
using System.Collections;
using System.Xml;

namespace Org.Reddragonit.EmbeddedWebServer
{
    /*
     * This class is a general utility class that contains commonly used methods and 
     * properties for the other classes within the library.
     */
    internal class Utility
    {
        public static readonly Version _OVERRIDE_VERSION = new Version("2.10");
        private static string basePath = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile.Substring(0, AppDomain.CurrentDomain.SetupInformation.ConfigurationFile.LastIndexOf(Path.DirectorySeparatorChar));

        //Called to locate a directory within the file system, starting with searching the base directory
        public static DirectoryInfo LocateDirectory(string name)
        {
            if (name == "/")
                return new DirectoryInfo((Site.CurrentSite!=null ? (Site.CurrentSite.BaseSitePath!=null ? Site.CurrentSite.BaseSitePath : basePath) : basePath));
            else
                return recurLocateDirectory(name, new DirectoryInfo((Site.CurrentSite != null ? (Site.CurrentSite.BaseSitePath != null ? Site.CurrentSite.BaseSitePath : basePath) : basePath)));
        }

        //The recursive portion of the above function
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

        //Called to locate a file by name using search operations
        public static FileInfo LocateFile(string name)
        {
            return recurLocateFile(name, new DirectoryInfo((Site.CurrentSite != null ? (Site.CurrentSite.BaseSitePath != null ? Site.CurrentSite.BaseSitePath : basePath) : basePath)));
        }

        //The recursive part of the above operation
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

        //Called to locate a type by its name, this scans through all assemblies 
        //which by default Type.Load does not perform.
        public static Type LocateType(string typeName)
        {
            Type t = Type.GetType(typeName, false, true);
            if (t == null)
            {
                foreach (Assembly ass in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        if (ass.GetName().Name != "mscorlib" && !ass.GetName().Name.StartsWith("System.") && ass.GetName().Name != "System" && !ass.GetName().Name.StartsWith("Microsoft"))
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

        //Called to locate all child classes of a given parent type
        public static List<Type> LocateTypeInstances(Type parent)
        {
            List<Type> ret = new List<Type>();
            foreach (Assembly ass in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (ass.GetName().Name != "mscorlib" && !ass.GetName().Name.StartsWith("System.") && ass.GetName().Name != "System" && !ass.GetName().Name.StartsWith("Microsoft"))
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

        //Called to locate all child classes of a given parent type for a specific assembly
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

        //called to open a stream of a given embedded resource, again searches through all assemblies
        public static Stream LocateEmbededResource(string name)
        {
            Stream ret = typeof(Utility).Assembly.GetManifestResourceStream(name);
            if (ret == null)
            {
                foreach (Assembly ass in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        if (ass.GetName().Name != "mscorlib" && !ass.GetName().Name.StartsWith("System.") && ass.GetName().Name != "System" && !ass.GetName().Name.StartsWith("Microsoft"))
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

        //returns a string containing the contents of an embedded resource
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

        //called to parse a java style properties file, such as the messages file
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

        //Called to compare two strings while handling null values
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

        //called to convert an object to an XML string
        public static string ConvertObjectToXML(object obj)
        {
            if (obj == null)
                return null;
            MemoryStream ms = new MemoryStream();
            string ret;
            if (obj is Hashtable)
                ret = SerializeHashtable((Hashtable)obj);
            else
            {
                XmlSerializer.FromTypes(new Type[] { obj.GetType() })[0].Serialize(ms, obj);
                ret = ASCIIEncoding.ASCII.GetString(ms.ToArray());
            }
            if (!ret.StartsWith("<"))
                ret = ret.Substring(ret.IndexOf("<"));
            return ret;
        }

        //implemented to serialize hash tables since it is not implemented in C#
        private static string SerializeHashtable(Hashtable ht)
        {
            MemoryStream ms = new MemoryStream();
            XmlWriter xw = XmlWriter.Create(ms);
            xw.WriteStartDocument();
            xw.WriteStartElement("hashtable");
            if (ht != null)
            {
                IDictionaryEnumerator ienum = ht.GetEnumerator();
                string tmp;
                while (ienum.MoveNext())
                {
                    xw.WriteStartElement("item");
                    xw.WriteStartElement("key");
                    xw.WriteStartAttribute("objecttype");
                    xw.WriteValue(ienum.Key.GetType().FullName);
                    xw.WriteEndAttribute();
                    tmp = ConvertObjectToXML(ienum.Key);
                    xw.WriteRaw(tmp.Substring(tmp.IndexOf(">") + 1));
                    xw.WriteEndElement();
                    xw.WriteStartElement("value");
                    if (ienum.Value == null)
                    {
                        xw.WriteStartAttribute("IsNull");
                        xw.WriteValue(true);
                        xw.WriteEndAttribute();
                    }
                    else
                    {
                        xw.WriteStartAttribute("IsNull");
                        xw.WriteValue(false);
                        xw.WriteEndAttribute();
                        xw.WriteStartAttribute("objecttype");
                        xw.WriteValue(ienum.Value.GetType().FullName);
                        xw.WriteEndAttribute();
                        tmp = ConvertObjectToXML(ienum.Value);
                        xw.WriteRaw(tmp.Substring(tmp.IndexOf(">") + 1));
                    }
                    xw.WriteEndElement();
                    xw.WriteEndElement();
                }
            }
            xw.WriteEndElement();
            xw.Flush();
            return ASCIIEncoding.ASCII.GetString(ms.ToArray());
        }

        //called to convert an objet from and XML string
        public static object ConvertObjectFromXML(Type type, string xmlCode)
        {
            if (xmlCode == null)
                return null;
            if (!xmlCode.StartsWith("<"))
                xmlCode = xmlCode.Substring(xmlCode.IndexOf("<"));
            if (type.FullName == typeof(Hashtable).FullName)
                return DeSerializeHashtable(xmlCode);
            return XmlSerializer.FromTypes(new Type[] { type })[0].Deserialize(new MemoryStream(ASCIIEncoding.ASCII.GetBytes(xmlCode)));
        }

        //implemented to deserialize hash tables since it is not implemented in C#
        private static Hashtable DeSerializeHashtable(string xmlCode)
        {
            Hashtable ret = new Hashtable();
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xmlCode);
            foreach (XmlNode node in doc.ChildNodes[1].ChildNodes)
            {
                object key = null;
                if (node.ChildNodes[0].Name == "hashtable")
                    key = DeSerializeHashtable("<?xml version=\"1.0\" encoding=\"us-ascii\"?>" + node.ChildNodes[0].InnerXml);
                else
                    key = ConvertObjectFromXML(LocateType(node.ChildNodes[0].Attributes[0].Value), "<?xml version=\"1.0\" encoding=\"us-ascii\"?>" + node.ChildNodes[0].InnerXml);
                object val = null;
                if (node.ChildNodes[1].Attributes["IsNull"] != null)
                {
                    if (!bool.Parse(node.ChildNodes[1].Attributes["IsNull"].Value))
                    {
                        if (node.ChildNodes[1].Name == "hashtable")
                            val = DeSerializeHashtable("<?xml version=\"1.0\" encoding=\"us-ascii\"?>" + node.ChildNodes[1].InnerXml);
                        else
                            val = ConvertObjectFromXML(LocateType(node.ChildNodes[1].Attributes["objecttype"].Value), "<?xml version=\"1.0\" encoding=\"us-ascii\"?>" + node.ChildNodes[1].InnerXml);
                    }
                }
                else
                {
                    if (node.ChildNodes[1].Name == "hashtable")
                        val = DeSerializeHashtable("<?xml version=\"1.0\" encoding=\"us-ascii\"?>" + node.ChildNodes[1].InnerXml);
                    else
                        val = ConvertObjectFromXML(LocateType(node.ChildNodes[1].Attributes["objecttype"].Value), "<?xml version=\"1.0\" encoding=\"us-ascii\"?>" + node.ChildNodes[1].InnerXml);
                }
                ret.Add(key, val);
            }
            return ret;
        }

        private static Version _monoVersion = null;
        internal static Version MonoVersion
        {
            get { return _monoVersion; }
        }

        static Utility()
        {
            Type type = Type.GetType("Mono.Runtime",false);
            if (type != null)
            {
                MethodInfo mi = type.GetMethod("GetDisplayName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                string str = mi.Invoke(null, new object[] { }).ToString();
                _monoVersion = new Version(str.Substring(0, str.IndexOf(" ")));
            }
        }

        internal static string TraceFullDirectoryPath(IDirectoryFolder folder)
        {
            string ret = "";
            while (folder != null)
            {
                ret = "/" + folder.Name + ret;
                folder = folder.Parent;
            }
            return ret;
        }

        internal static string TraceFullFilePath(IDirectoryFile file)
        {
            return TraceFullDirectoryPath(file.Folder) + "/" + file.Name;
        }
    }
}
