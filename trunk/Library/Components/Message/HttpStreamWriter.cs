using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Org.Reddragonit.EmbeddedWebServer.Components.Message
{
    public class HttpStreamWriter : StreamWriter
    {
        private Encoding _enc;
        private BinaryWriter _bw;

        internal HttpStreamWriter(Stream stream,Encoding enc)
            : base(stream)
        {
            _enc = enc;
            _bw = new BinaryWriter(stream);
        }

        public new void Write(string value) {
            _bw.Write(_enc.GetBytes(value));
        }

        public new void Write(string format, object arg0)
        {
            this.Write(string.Format(format, arg0));
        }

        public new void Write(string format,params object[] arg){
            this.Write(string.Format(format,arg));
        }

        public new void Write(string format, object arg0,object arg1)
        {
            this.Write(string.Format(format, arg0,arg1));
        }

        public new void Write(string format, object arg0, object arg1,object arg2)
        {
            this.Write(string.Format(format, arg0, arg1,arg2));
        }

        public new void WriteLine(string value)
        {
            _bw.Write(_enc.GetBytes(value+this.NewLine));
        }

        public new void WriteLine(string format, object arg0)
        {
            this.WriteLine(string.Format(format, arg0));
        }

        public new void WriteLine(string format, params object[] arg)
        {
            this.WriteLine(string.Format(format, arg));
        }

        public new void WriteLine(string format, object arg0, object arg1)
        {
            this.WriteLine(string.Format(format, arg0, arg1));
        }

        public new void WriteLine(string format, object arg0, object arg1, object arg2)
        {
            this.WriteLine(string.Format(format, arg0, arg1, arg2));
        }
    }
}
