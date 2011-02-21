using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.IO;

namespace Org.Reddragonit.EmbeddedWebServer
{
    internal class Messages
    {
        private static object _lock = new object();

        private static Messages _current;
        public static Messages Current
        {
            get
            {
                Monitor.Enter(_lock);
                if (_current == null)
                    _current = new Messages();
                Monitor.Exit(_lock);
                return _current;
            }
        }

        private Dictionary<string, string> _messages;

        private Messages()
        {
            _messages = new Dictionary<string, string>();
            _messages = Utility.ParseProperties(Utility.ReadEmbeddedResource("Org.Reddragonit.EmbeddedWebServer.DefaultMessages.properties"));
            if (Settings.MessagesFilePath != null)
            {
                StreamReader sr = new StreamReader(new FileStream(Settings.MessagesFilePath, FileMode.Open, FileAccess.Read, FileShare.None));
                string stmp = sr.ReadToEnd();
                sr.Close();
                Dictionary<string, string> tmp = Utility.ParseProperties(stmp);
                foreach (string str in tmp.Keys)
                {
                    if (_messages.ContainsKey(str))
                        _messages.Remove(str);
                    _messages.Add(str, tmp[str]);
                }
            }
        }

        public string this[string name]
        {
            get
            {
                if (_messages.ContainsKey(name))
                    return _messages[name];
                return null;
            }
        }
    }
}
