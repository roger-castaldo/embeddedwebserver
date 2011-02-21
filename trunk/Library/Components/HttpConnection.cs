using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.IO;
using System.Collections;
using System.Threading;
using System.Web;
using System.Collections.Specialized;

namespace Org.Reddragonit.EmbeddedWebServer.Components
{
    public class HttpConnection
    {
        public TcpClient socket;        

        private Stream inputStream;
        public StreamWriter outputStream;

        private string _method;
        public string Method
        {
            get { return _method; }
        }

        private Uri _url;
        public Uri URL
        {
            get { return _url; }
        }

        private string _version;
        public string Version
        {
            get { return _version; }
        }

        private Dictionary<string, string> _requestHeaders;
        public Dictionary<string, string> RequestHeaders
        {
            get { return _requestHeaders; }
        }

        private Dictionary<string, string> _requestParameters;
        public Dictionary<string, string> RequestParameters
        {
            get {
                if (_requestParameters == null)
                    parseParameters();
                return _requestParameters; 
            }
        }

        private void parseParameters()
        {
            _requestParameters = new Dictionary<string, string>();
            if (URL.Query != null)
            {
                NameValueCollection col = HttpUtility.ParseQueryString(URL.Query);
                foreach (string str in col.Keys){
                    _requestParameters.Add(str, col[str]);
                }
            }

        }

        private static int MAX_POST_SIZE = 10 * 1024 * 1024; // 10MB

        public HttpConnection(TcpClient s) {
            this.socket = s;
            inputStream = new BufferedStream(socket.GetStream());

            // we probably shouldn't be using a streamwriter for all output from handlers either
            outputStream = new StreamWriter(new BufferedStream(socket.GetStream()));
            try
            {
                parseRequest();
            }
            catch (Exception e) {
                throw e;
            }
        }
        

        private string streamReadLine(Stream inputStream) {
            int next_char;
            string data = "";
            while (true) {
                next_char = inputStream.ReadByte();
                if (next_char == '\n') { break; }
                if (next_char == '\r') { continue; }
                if (next_char == -1) { Thread.Sleep(1); continue; };
                data += Convert.ToChar(next_char);
            }            
            return data;
        }

        private void parseRequest() {
            String request = streamReadLine(inputStream);
            string[] tokens = request.Split(' ');
            if (tokens.Length != 3) {
                throw new Exception("invalid http request line");
            }
            _method = tokens[0].ToUpper();
            _version = tokens[2];
            Console.WriteLine("readHeaders()");
            String line;
            _requestHeaders = new Dictionary<string, string>();
            while ((line = streamReadLine(inputStream)) != null)
            {
                if (line.Equals(""))
                {
                    Console.WriteLine("got headers");
                    return;
                }

                int separator = line.IndexOf(':');
                if (separator == -1)
                {
                    throw new Exception("invalid http header line: " + line);
                }
                String name = line.Substring(0, separator);
                int pos = separator + 1;
                while ((pos < line.Length) && (line[pos] == ' '))
                {
                    pos++; // strip any spaces
                }

                string value = line.Substring(pos, line.Length - pos);
                Console.WriteLine("header: {0}:{1}", name, value);
                _requestHeaders.Add(name, value);
            }
            _url = new Uri(_requestHeaders["Host"] + tokens[1]);
        }

        /*private const int BUF_SIZE = 4096;
        public void handlePOSTRequest() {
            // this post data processing just reads everything into a memory stream.
            // this is fine for smallish things, but for large stuff we should really
            // hand an input stream to the request processor. However, the input stream 
            // we hand him needs to let him see the "end of the stream" at this content 
            // length, because otherwise he won't know when he's seen it all! 

            Console.WriteLine("get post data start");
            int content_len = 0;
            MemoryStream ms = new MemoryStream();
            if (this.RequestHeaders.ContainsKey("Content-Length")) {
                 content_len = Convert.ToInt32(this.RequestHeaders["Content-Length"]);
                 if (content_len > MAX_POST_SIZE) {
                     throw new Exception(
                         String.Format("POST Content-Length({0}) too big for this simple server",
                           content_len));
                 }
                 byte[] buf = new byte[BUF_SIZE];              
                 int to_read = content_len;
                 while (to_read > 0) {  
                     Console.WriteLine("starting Read, to_read={0}",to_read);

                     int numread = this.inputStream.Read(buf, 0, Math.Min(BUF_SIZE, to_read));
                     Console.WriteLine("read finished, numread={0}", numread);
                     if (numread == 0) {
                         if (to_read == 0) {
                             break;
                         } else {
                             throw new Exception("client disconnected during post");
                         }
                     }
                     to_read -= numread;
                     ms.Write(buf, 0, numread);
                 }
                 ms.Seek(0, SeekOrigin.Begin);
            }
            Console.WriteLine("get post data end");
            srv.handlePOSTRequest(this, new StreamReader(ms));

        }*/

        public void writeSuccess() {
            outputStream.Write("HTTP/1.0 200 OK\n");
            outputStream.Write("Content-Type: text/html\n");
            outputStream.Write("Connection: close\n");
            outputStream.Write("\n");
        }

        public void writeFailure() {
            outputStream.Write("HTTP/1.0 404 File not found\n");
            outputStream.Write("Connection: close\n");
            outputStream.Write("\n");
        }
    }
}
