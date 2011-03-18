using System;
using System.Collections.Generic;
using System.Text;

namespace Org.Reddragonit.EmbeddedWebServer.Components
{
    /*
     * This is a storage class used in all caches.
     * It simply contains an object and the last access,
     * the last access being used to know when to destroy a 
     * cached object from the queue.
     */
    public class CachedItemContainer
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

        public override bool Equals(object obj)
        {
            return Value.Equals(((CachedItemContainer)obj).Value);
        }
    }
}
