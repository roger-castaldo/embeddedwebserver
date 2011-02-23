using System;
using System.Collections.Generic;
using System.Text;

namespace Org.Reddragonit.EmbeddedWebServer.Cache
{
    internal class CachedItemContainer
    {
        private DateTime _lastAccess;
        public DateTime LastAccess
        {
            get { return _lastAccess; }
        }

        private object _value;
        public object Value
        {
            get {
                _lastAccess = DateTime.Now;
                return _value; 
            }
            set
            {
                _lastAccess = DateTime.Now;
                _value = value;
            }
        }

        public CachedItemContainer(object value)
        {
            _lastAccess = DateTime.Now;
            _value = value;
        }
    }
}
