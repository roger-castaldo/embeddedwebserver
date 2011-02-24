using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.IO;
using System.Collections;
using System.Threading;
using System.Collections.Specialized;
using Org.Reddragonit.EmbeddedWebServer.Interfaces;
using Org.Reddragonit.EmbeddedWebServer.Sessions;
using System.Net;
using Procurios.Public;
using Org.Reddragonit.EmbeddedWebServer.Diagnostics;

namespace Org.Reddragonit.EmbeddedWebServer.Components
{
    public class HttpConnection
    {

        private const int BUF_SIZE = 4096;
        private static int MAX_POST_SIZE = 10 * 1024 * 1024; // 10MB

        private TcpClient socket;
        public EndPoint Client
        {
            get { return socket.Client.RemoteEndPoint; }
        }

        public EndPoint LocalEndPoint
        {
            get { return socket.Client.LocalEndPoint; }
        }

        private Stream inputStream;

        private Stream _outStream;

        private StreamWriter _responseWriter;
        public StreamWriter ResponseWriter
        {
            get { return _responseWriter; }
        }

        private SessionState _session;
        public SessionState Session
        {
            get { return _session; }
        }

        internal void SetSession(SessionState session)
        {
            _session = session;
        }

        public HttpConnection(TcpClient s)
        {
            DateTime start = DateTime.Now;
            this.socket = s;
            inputStream = new BufferedStream(socket.GetStream());

            // we probably shouldn't be using a streamwriter for all output from handlers either
            _outStream = new MemoryStream();
            _responseWriter = new StreamWriter(_outStream);
            _responseHeaders = new HeaderCollection();
            _responseHeaders["Server"] = Messages.Current["Org.Reddragonit.EmbeddedWebServer.DefaultHeaders.Server"];
            _responseStatus = HttpStatusCodes.OK;
            _responseCookie = new CookieCollection();
            try
            {
                parseRequest();
            }
            catch (Exception e)
            {
                throw e;
            }
            Logger.LogMessage(DiagnosticsLevels.TRACE, "Total time to load request: " + DateTime.Now.Subtract(start).TotalMilliseconds.ToString() + "ms");
        }

        #region Request

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

        private HeaderCollection _requestHeaders;
        public HeaderCollection RequestHeaders
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

        private Dictionary<string, UploadedFile> _uploadedFiles;
        public Dictionary<string, UploadedFile> UploadedFiles
        {
            get {
                if (_uploadedFiles == null)
                    parseParameters();
                return _uploadedFiles; 
            }
        }

        private CookieCollection _requestCookie;
        public CookieCollection RequestCookie
        {
            get { return _requestCookie; }
        }

        private object _jsonParameter=null;
        public object JSONParameter
        {
            get { return _jsonParameter; }
        }

        private void parseParameters()
        {
            _requestParameters = new Dictionary<string, string>();
            _uploadedFiles = new Dictionary<string, UploadedFile>();
            if (URL.Query != null)
            {
                string query = HttpUtility.UrlDecode(URL.Query);
                if (query.StartsWith("?{")&&query.EndsWith("}"))
                {
                    query = query.Substring(1);
                    if (query != "{}")
                        _jsonParameter = JSON.JsonDecode(query);
                }
                else
                {
                    NameValueCollection col = HttpUtility.ParseQueryString(query);
                    foreach (string str in col.Keys)
                    {
                        _requestParameters.Add(str, col[str]);
                    }
                }
            }
            if (_requestHeaders.ContentLength != null)
            {
                MemoryStream ms = new MemoryStream();
                int content_len = Convert.ToInt32(_requestHeaders.ContentLength);
                if (content_len > MAX_POST_SIZE)
                {
                    throw new Exception(
                        String.Format("POST Content-Length({0}) too big for this simple server",
                          content_len));
                }
                byte[] buf = new byte[BUF_SIZE];
                int to_read = content_len;
                while (to_read > 0)
                {
                    int numread = this.inputStream.Read(buf, 0, Math.Min(BUF_SIZE, to_read));
                    if (numread == 0)
                    {
                        if (to_read == 0)
                        {
                            break;
                        }
                        else
                        {
                            throw new Exception("client disconnected during post");
                        }
                    }
                    to_read -= numread;
                    ms.Write(buf, 0, numread);
                }
                ms.Seek(0, SeekOrigin.Begin);
                if (_requestHeaders.ContentType.StartsWith("multipart/form-data"))
                {
                    if (_requestHeaders.ContentType.Contains("boundary="))
                    {
                        string boundary = "--"+_requestHeaders.ContentType.Substring(_requestHeaders.ContentType.IndexOf("boundary=") + "boundary=".Length).Replace("-","");
                        string line;
                        string var;
                        string value;
                        string fileName;
                        while ((line = streamReadLine(ms)) != null)
                        {
                            if (line.EndsWith(boundary + "--"))
                                break;
                            else if (line.EndsWith(boundary))
                            {
                                line = streamReadLine(ms);
                                if (line.Contains("filename="))
                                {
                                    var = line.Substring(line.IndexOf("name=\"") + "name=\"".Length);
                                    var = var.Substring(0, var.IndexOf("\";"));
                                    fileName = line.Substring(line.IndexOf("filename=\"") + "filename=\"".Length);
                                    fileName = fileName.Substring(0, fileName.Length - 1);
                                    string contentType = streamReadLine(ms);
                                    contentType = contentType.Substring(contentType.IndexOf(":")+1);
                                    contentType=contentType.Trim();
                                    streamReadLine(ms);
                                    MemoryStream str = new MemoryStream();
                                    BinaryWriter br = new BinaryWriter(str);
                                    while ((line = PeakLine(ms)) != null)
                                    {
                                        if (line.EndsWith(boundary) || line.EndsWith(boundary + "--"))
                                            break;
                                        else
                                            br.Write(line.ToCharArray());
                                        streamReadLine(ms);
                                    }
                                    br.Flush();
                                    str.Seek(0, SeekOrigin.Begin);
                                    _uploadedFiles.Add(var, new UploadedFile(var, fileName, contentType, str));
                                }
                                else
                                {
                                    var = line.Substring(line.IndexOf("name=\"") + "name=\"".Length);
                                    var = var.Substring(0, var.Length-1);
                                    streamReadLine(ms);
                                    value = "";
                                    while ((line = PeakLine(ms)) != null)
                                    {
                                        if (line.EndsWith(boundary)||line.EndsWith(boundary+"--"))
                                            break;
                                        else
                                            value += streamReadLine(ms);
                                    }
                                    _requestParameters.Add(var, value.Trim());
                                }
                            }
                        }
                    }else
                        throw new Exception("Unknown format, content-type: " + _requestHeaders.ContentType + " unable to parse in parameters.");
                }
                else if (_requestHeaders.ContentType == "application/x-www-form-urlencoded")
                {
                    string postData = new StreamReader(ms).ReadToEnd();
                    string query = HttpUtility.UrlDecode(postData);
                    if (query.StartsWith("?{") && query.EndsWith("}"))
                    {
                        query = query.Substring(1);
                        if (query != "{}")
                            _jsonParameter = JSON.JsonDecode(query);
                    }
                    else
                    {
                        NameValueCollection col = HttpUtility.ParseQueryString(postData);
                        foreach (string str in col.Keys)
                        {
                            _requestParameters.Add(str, col[str]);
                        }
                    }
                }
                else
                    throw new Exception("Unknown format, content-type: " + _requestHeaders.ContentType + " unable to parse in parameters.");
            }
        }

        private void parseRequest()
        {
            String request = streamReadLine(inputStream);
            string[] tokens = request.Split(' ');
            if (tokens.Length != 3)
            {
                throw new Exception("invalid http request line");
            }
            _method = tokens[0].ToUpper();
            _version = tokens[2];
            String line;
            _requestHeaders = new HeaderCollection();
            while ((line = streamReadLine(inputStream)) != null)
            {
                if (line.Equals(""))
                {
                    break;
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
                _requestHeaders[name] = value;
            }
            _url = new Uri("http://" + _requestHeaders.Host + tokens[1]);
            _requestCookie = new CookieCollection(_requestHeaders["Cookie"]);
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

        private string PeakLine(Stream inputStream)
        {
            long start = inputStream.Position;
            string ret = streamReadLine(inputStream);
            inputStream.Seek(start, SeekOrigin.Begin);
            return ret;
        }
        #endregion

        #region Response
        public void UseResponseStream(Stream str)
        {
            _outStream = str;
        }

        public void ClearResponse()
        {
            _responseWriter = null;
            _outStream = new MemoryStream();
            _responseWriter = new StreamWriter(_outStream);
        }


        public void SendResponse()
        {
            ResponseWriter.Flush();
            _responseHeaders.ContentLength = _outStream.Length.ToString();
            if (_responseHeaders["Accept-Ranges"] == null)
                _responseHeaders["Accept-Ranges"] = Messages.Current["Org.Reddragonit.EmbeddedWebServer.DefaultHeaders.AcceptRanges"];
            if (_responseHeaders.ContentType == null)
                _responseHeaders.ContentType = Messages.Current["Org.Reddragonit.EmbeddedWebServer.DefaultHeaders.ContentType"];
            if (_responseHeaders["Server"]==null)
                _responseHeaders["Server"] = Messages.Current["Org.Reddragonit.EmbeddedWebServer.DefaultHeaders.Server"];
            if (_responseHeaders.Date == null)
                _responseHeaders.Date = DateTime.Now.ToString("r");
            _responseHeaders["Connection"] = "close";
            Stream outStream = socket.GetStream();
            string line = "HTTP/1.0 " + ((int)ResponseStatus).ToString() + " " + ResponseStatus.ToString().Replace("_", "") + "\n";
            foreach (string str in _responseHeaders.Keys)
                line+=str + ": " + _responseHeaders[str]+"\n";
            if (_responseCookie != null)
            {
                foreach (string str in _responseCookie.Keys)
                {
                    line += "Set-Cookie: " + str + "=" + _responseCookie[str] + "; Expires=" + _responseCookie.Expiry.ToString("r")+"\n";
                }
            }
            line += "\n";
            outStream.Write(System.Text.ASCIIEncoding.ASCII.GetBytes(line), 0, line.Length);
            byte[] buffer = new byte[socket.Client.SendBufferSize];
            Logger.LogMessage(DiagnosticsLevels.TRACE,"Sending Buffer size: " + socket.Client.SendBufferSize.ToString());
            _outStream.Seek(0, SeekOrigin.Begin);
            Logger.LogMessage(DiagnosticsLevels.TRACE, "Size of data to send: " + _outStream.Length.ToString());
            while (_outStream.Position < _outStream.Length)
            {
                int len = _outStream.Read(buffer,0,(int)Math.Min(socket.Client.SendBufferSize, (int)(_outStream.Length - _outStream.Position)));
                Logger.LogMessage(DiagnosticsLevels.TRACE, "Length of data chunk to send: " + len.ToString());
                outStream.Write(buffer, 0, len);
            }
            socket.Close();
        }

        private HeaderCollection _responseHeaders;
        public HeaderCollection ResponseHeaders
        {
            get { return _responseHeaders; }
            set { _responseHeaders = value; }
        }

        private HttpStatusCodes _responseStatus;
        public HttpStatusCodes ResponseStatus
        {
            get { return _responseStatus; }
            set { _responseStatus = value; }
        }

        private CookieCollection _responseCookie;
        public CookieCollection ResponseCookie
        {
            get { return _responseCookie; }
            set { _responseCookie = value; }
        }
        #endregion
    }
}
