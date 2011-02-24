using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.IO;
using System.Xml.Serialization;

namespace Org.Reddragonit.EmbeddedWebServer.Sessions
{
    public class SessionState
    {
        private string _id;
        public string ID
        {
            get { return _id; }
        }

        private DateTime _expiry;
        public DateTime Expiry
        {
            get { return _expiry; }
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

        internal SessionState(string id)
        {
            _id = id;
            _expiry = DateTime.Now;
            _content = new Dictionary<string, object>();
        }

        internal void Renew(int minutes)
        {
            _expiry = DateTime.Now.AddMinutes(minutes);
        }

        internal void StoreToFile(string path)
        {
            XmlWriter xwrite = XmlWriter.Create(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None));
            xwrite.WriteStartDocument();
            xwrite.WriteStartElement("Session");
            
            xwrite.WriteStartAttribute("ID");
            xwrite.WriteValue(ID);
            xwrite.WriteEndAttribute();

            xwrite.WriteStartAttribute("Expiry");
            xwrite.WriteValue(Expiry);
            xwrite.WriteEndAttribute();

            xwrite.WriteStartElement("Objects");

            foreach (string str in _content.Values)
            {
                if (_content[str] != null)
                {
                    xwrite.WriteStartElement("Object");

                    xwrite.WriteStartAttribute("Name");
                    xwrite.WriteValue(str);
                    xwrite.WriteEndElement();

                    xwrite.WriteStartAttribute("Type");
                    xwrite.WriteValue(_content[str].GetType().FullName);
                    xwrite.WriteEndAttribute();

                    xwrite.WriteStartElement("Value");
                    xwrite.WriteRaw(Utility.ConvertObjectToXML(_content[str]));
                    xwrite.WriteEndElement();

                    xwrite.WriteEndElement();
                }
            }

            xwrite.WriteEndElement();

            xwrite.WriteEndElement();
            xwrite.WriteEndDocument();
            xwrite.Flush();
            xwrite.Close();
        }

        internal void LoadFromFile(string path)
        {
            XmlDocument doc = new XmlDocument();
            Stream str = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            doc.Load(str);
            str.Close();
            _id = doc.GetElementsByTagName("Session")[0].Attributes["ID"].Value;
            _expiry = DateTime.Parse(doc.GetElementsByTagName("Session")[0].Attributes["Expiry"].Value);
            foreach (XmlNode onode in doc.GetElementsByTagName("Objects")[0].ChildNodes)
            {
                string name = onode.Attributes["Name"].Value;
                Type t = Utility.LocateType(onode.Attributes["Type"].Value);
                _content.Add(name, Utility.ConvertObjectFromXML(t,onode.InnerXml));
            }
        }

        internal static DateTime GetExpiryFromFile(string path)
        {
            FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            XmlReader read = XmlReader.Create(fs);
            read.Skip();
            read.Skip();
            read.Skip();
            DateTime ret = DateTime.Parse(read.Value);
            read.Close();
            fs.Close();
            return ret;
        }
    }
}
