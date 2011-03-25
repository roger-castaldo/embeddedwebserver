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
    /*
     * This class is a wrapper for the http connection established by
     * the client.  It houses the underlying socket as well as 
     * the cookie and header information.
     */
    public class HttpConnection
    {
        //A thread specific instance of the current connection
        [ThreadStatic()]
        private static HttpConnection _currentConnection;
        public static HttpConnection CurrentConnection
        {
            get { return _currentConnection; }
        }

        //called to set the current connection for the thread
        internal static void SetCurrentConnection(HttpConnection conn)
        {
            _currentConnection = conn;
        }

        //the basic buffer size to use when reading/writing data
        private const int BUF_SIZE = 4096;
        //the maximium size of a post data allowed
        private static int MAX_POST_SIZE = 10 * 1024 * 1024; // 10MB

        //the underlying socket for the connection
        private TcpClient socket;

        //returns the client endpoint information
        public EndPoint Client
        {
            get { return socket.Client.RemoteEndPoint; }
        }

        //returns the local endpoint information
        public EndPoint LocalEndPoint
        {
            get { return socket.Client.LocalEndPoint; }
        }

        //houses the input stream used to read in all supplied client information
        private Stream inputStream;

        //houses the outgoing buffer to write out information
        private Stream _outStream;

        //used to access the response buffer in order to write the response to it
        private StreamWriter _responseWriter;
        public StreamWriter ResponseWriter
        {
            get { return _responseWriter; }
        }

        //houses the http sessions for the connection if loaded
        private SessionState _session;
        public SessionState Session
        {
            get { return _session; }
        }

        //called to set the session for the current connection
        internal void SetSession(SessionState session)
        {
            _session = session;
        }

        //houses connection specific variables that can be set along the way 
        //and are only valid while the connection is being processed
        private Dictionary<string, object> _contextVariables;
        public object this[string ConnectionVariableName]
        {
            get {
                if (_contextVariables != null)
                {
                    if (_contextVariables.ContainsKey(ConnectionVariableName))
                        return _contextVariables[ConnectionVariableName];
                }
                return null;
            }
            set {
                if (_contextVariables == null)
                    _contextVariables = new Dictionary<string, object>();
                if (_contextVariables.ContainsKey(ConnectionVariableName))
                    _contextVariables.Remove(ConnectionVariableName);
                if (value != null)
                    _contextVariables.Add(ConnectionVariableName, value);
            }
        }

        /*
         * This constructor loads and http connection from a given tcp client.
         * It establishes the required streams and objects, then loads in the 
         * header information, it avoids loading in post data for efficeincy.
         * The post data gets loaded later on when the parameters are accessed.
         */
        public HttpConnection(TcpClient s)
        {
            DateTime start = DateTime.Now;
            this.socket = s;
            inputStream = new BufferedStream(socket.GetStream());

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

        //returns the method specified by the request
        private string _method;
        public string Method
        {
            get { return _method; }
        }

        //returns the url that the request is attempting to access
        private Uri _url;
        public Uri URL
        {
            get { return _url; }
        }

        internal void UseDefaultPath(Site site)
        {
            _url = new Uri("http://" + _url.Host + site.DefaultPage);
        }

        //returns the http version specified in the request
        private string _version;
        public string Version
        {
            get { return _version; }
        }

        //houses the headers specified in the request
        private HeaderCollection _requestHeaders;
        public HeaderCollection RequestHeaders
        {
            get { return _requestHeaders; }
        }

        //houses the parameters specified in the request.  They are loaded at the point
        //that this property is called the first time to be more efficient
        private ParameterCollection _requestParameters;
        public ParameterCollection RequestParameters
        {
            get {
                if (_requestParameters == null)
                    parseParameters();
                return _requestParameters; 
            }
        }

        //houses all the uploaded file information from the request.  These are loaded 
        //at the point that this property is called the first time, or when the 
        //request parameters are loaded the first time.
        private UploadedFileCollection _uploadedFiles;
        public UploadedFileCollection UploadedFiles
        {
            get {
                if (_uploadedFiles == null)
                    parseParameters();
                return _uploadedFiles; 
            }
        }

        //houses the cookie information specified by the request this is loaded with the headers
        private CookieCollection _requestCookie;
        public CookieCollection RequestCookie
        {
            get { return _requestCookie; }
        }

        //houses the json object specified in the query, typically used by the embedded services
        private object _jsonParameter=null;
        public object JSONParameter
        {
            get {
                if (_requestParameters == null)
                    parseParameters();
                return _jsonParameter; 
            }
        }

        /*
         * This function parses the parameters that were sent by the client.
         * It first attempts to parse out a query string, if specified, including any
         * json objects.  It then attempts to parse any posted data including uploaded
         * files and again json data, if any.
         */
        private void parseParameters()
        {
            Dictionary<string,string> requestParameters = new Dictionary<string, string>();
            Dictionary<string,UploadedFile> uploadedFiles = new Dictionary<string, UploadedFile>();
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
                        requestParameters.Add(str, col[str]);
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
                                    uploadedFiles.Add(var, new UploadedFile(var, fileName, contentType, str));
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
                                    requestParameters.Add(var, value.Trim());
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
                            requestParameters.Add(str, col[str]);
                        }
                    }
                }
                else
                    throw new Exception("Unknown format, content-type: " + _requestHeaders.ContentType + " unable to parse in parameters.");
            }
            _requestParameters = new ParameterCollection(requestParameters);
            _uploadedFiles = new UploadedFileCollection(uploadedFiles);
        }

        /*
         * This function is used to parse the headers/cookie portion
         * of the request submitted to the client socket.
         */
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

        //an internal function used to read a line from the stream to prevent encoding issues
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

        //an internal function to peak a line out of the stream to prevent encoding issues
        private string PeakLine(Stream inputStream)
        {
            long start = inputStream.Position;
            string ret = streamReadLine(inputStream);
            inputStream.Seek(start, SeekOrigin.Begin);
            return ret;
        }
        #endregion

        #region Response
        //called to override the response buffer stream with an internally created one
        public void UseResponseStream(Stream str)
        {
            _outStream = str;
        }

        //clears all response data and produces a new response buffer
        public void ClearResponse()
        {
            _responseWriter = null;
            _outStream = new MemoryStream();
            _responseWriter = new StreamWriter(_outStream);
        }

        /*
         * This function sends the response back to the client.  It flushes the 
         * writer is necessary.  Then proceeds to build a full response in a string buffer 
         * including writing out all the headers and cookie information specified.  It then appends
         * the actual outstream data to the buffer.  Once complete, it reads that buffer 
         * and sends back the data in chunks of data sized by the client buffer size specification.
         */
        public void SendResponse()
        {
            ResponseWriter.Flush();
            DateTime start = DateTime.Now;
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
                if (Site.CurrentSite != null)
                    _responseCookie.Renew(Site.CurrentSite.CookieExpireMinutes);
                foreach (string str in _responseCookie.Keys)
                {
                    line += "Set-Cookie: " + str + "=" + _responseCookie[str] + "; Expires=" + _responseCookie.Expiry.ToString("r")+"\n";
                }
            }
            line += "\n";
            outStream.Write(System.Text.ASCIIEncoding.ASCII.GetBytes(line), 0, line.Length);
            Logger.LogMessage(DiagnosticsLevels.TRACE, "Time to send headers for URL " + this.URL.AbsolutePath + " = " + DateTime.Now.Subtract(start).TotalMilliseconds.ToString() + "ms");
            start = DateTime.Now;
            byte[] buffer = new byte[socket.Client.SendBufferSize];
            Logger.LogMessage(DiagnosticsLevels.TRACE,"Sending Buffer size: " + socket.Client.SendBufferSize.ToString());
            _outStream.Seek(0, SeekOrigin.Begin);
            Logger.LogMessage(DiagnosticsLevels.TRACE, "Size of data to send: " + _outStream.Length.ToString());
            while (_outStream.Position < _outStream.Length)
            {
                Logger.LogMessage(DiagnosticsLevels.TRACE, "Length of data chunk to read from buffer: " + ((int)Math.Min(buffer.Length, (int)(_outStream.Length - _outStream.Position))).ToString());
                int len = _outStream.Read(buffer, 0, (int)Math.Min(buffer.Length, (int)(_outStream.Length - _outStream.Position)));
                Logger.LogMessage(DiagnosticsLevels.TRACE, "Length of data chunk to send: " + len.ToString());
                try
                {
                    outStream.Write(buffer, 0, len);
                }
                catch (Exception e)
                {
                    _outStream.Position = _outStream.Length;
                }
            }
            try
            {
                socket.Close();
            }
            catch (Exception e) { }
            Logger.LogMessage(DiagnosticsLevels.TRACE, "Time to send response content for URL " + this.URL.AbsolutePath + " = " + DateTime.Now.Subtract(start).TotalMilliseconds.ToString() + "ms");
        }

        //houses the headers used in the response
        private HeaderCollection _responseHeaders;
        public HeaderCollection ResponseHeaders
        {
            get { return _responseHeaders; }
            set { _responseHeaders = value; }
        }

        //houses the response status
        private HttpStatusCodes _responseStatus;
        public HttpStatusCodes ResponseStatus
        {
            get { return _responseStatus; }
            set { _responseStatus = value; }
        }

        //houses the response cookies
        private CookieCollection _responseCookie;
        public CookieCollection ResponseCookie
        {
            get { return _responseCookie; }
            set { _responseCookie = value; }
        }
        #endregion
    }
}
