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
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

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
        private static HttpConnection _currentConnection = null;
        internal static void SetCurrentConnection(HttpConnection con)
        {
            _currentConnection = con;
        }
        public static HttpConnection CurrentConnection
        {
            get { return _currentConnection; }
        }

        //the basic buffer size to use when reading/writing data
        private const int BUF_SIZE = 4096;
        //the maximum time to wait for a header
        private const int MAX_HEADER_WAIT_TIME = 2000; //ms
        //the maximium size of a post data allowed
        private static int MAX_POST_SIZE = 10 * 1024 * 1024; // 10MB

        //the underlying socket for the connection
        private TcpClient socket;

        //holds whether or not the response was already send
        private bool _isResponseSent;
        public bool IsResponseSent
        {
            get { return _isResponseSent; }
        }

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

        //returns the IPPortPair that the connection was made on
        private sIPPortPair _listener;
        public sIPPortPair Listener
        {
            get { return _listener; }
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

        //used to buffer incoming data
        private StringBuilder _sbuffer;
        //used to house the data returned on an async read from the input stream
        private byte[] _buffer;
        private const int _BUFFER_SIZE = 512;

        //event triggered when header is complete
        private ManualResetEvent _mreHeader;
        private ManualResetEvent _mreContent;
        private ManualResetEvent _mreHeaderParsed;
        private bool _headerRecieved;
        private bool _requestComplete;
        private string _header;
        private string _content;
        private Thread _backgroundReaderThread;
        private bool _exit = false;

        private void backgroundRunStart()
        {
            _currentConnection = this;
            _buffer = new byte[_BUFFER_SIZE];
            inputStream.ReadTimeout = 100;
            while (!_exit)
            {
                try
                {
                    int len = inputStream.Read(_buffer, 0, _BUFFER_SIZE);
                    if (len > 0)
                    {
                        Logger.LogMessage(DiagnosticsLevels.TRACE, "Appended chunk of data of length " + len.ToString() + " to the socket buffer [id:" + _id.ToString() + "]");
                        _sbuffer.Append(ASCIIEncoding.ASCII.GetString(_buffer, 0, len));
                        if (_sbuffer.ToString().Contains("\r\n\r\n")
                            || (_headerRecieved && !_requestComplete))
                        {
                            if (!_headerRecieved)
                            {
                                Logger.LogMessage(DiagnosticsLevels.TRACE, "Header recieved for connection [id:" + _id.ToString() + "]");
                                _headerRecieved = true;
                                _header = _sbuffer.ToString().Substring(0, _sbuffer.ToString().IndexOf("\r\n\r\n"));
                                _sbuffer.Replace(_header + "\r\n\r\n", "");
                                Logger.LogMessage(DiagnosticsLevels.TRACE, "Connection header received: [id:" + _id.ToString() + "]" + _header);
                                _mreHeader.Set();
                                _mreHeaderParsed.WaitOne();
                                if (_requestHeaders.ContentLength != null)
                                {
                                    if (_sbuffer.Length == int.Parse(_requestHeaders.ContentLength))
                                    {
                                        _content = _sbuffer.ToString().TrimEnd(new char[] { '\r', '\n' });
                                        _requestComplete = true;
                                        _mreContent.Set();
                                    }
                                }
                                else if (_sbuffer.ToString() == "{}")
                                {
                                    _content = _sbuffer.ToString().TrimEnd(new char[] { '\r', '\n' });
                                    _requestComplete = true;
                                    _mreContent.Set();
                                }
                            }
                            else
                            {
                                if (_requestHeaders.ContentLength != null)
                                {
                                    if (_sbuffer.Length == int.Parse(_requestHeaders.ContentLength))
                                    {
                                        _content = _sbuffer.ToString().TrimEnd(new char[] { '\r', '\n' });
                                        _requestComplete = true;
                                        _mreContent.Set();
                                    }
                                }
                                else if (_sbuffer.ToString().Contains("\r\n\r\n"))
                                {
                                    _content = _sbuffer.ToString().TrimEnd(new char[] { '\r', '\n' });
                                    _requestComplete = true;
                                    _mreContent.Set();
                                }
                            }
                        }
                    }
                }
                catch (Exception e) { }
                if (!_headerRecieved || !_requestComplete)
                    _buffer = new byte[_BUFFER_SIZE];
                else
                    _exit = true;
            }
        }

        private DateTime _requestStart;
        public DateTime RequestStart
        {
            get { return _requestStart; }
        }

        private long _id;
        public long ID
        {
            get { return _id; }
        }

        /*
         * This constructor loads and http connection from a given tcp client.
         * It establishes the required streams and objects, then loads in the 
         * header information, it avoids loading in post data for efficeincy.
         * The post data gets loaded later on when the parameters are accessed.
         */
        internal HttpConnection(TcpClient s, sIPPortPair listener, X509Certificate cert,long id)
        {
            _id = id;
            _currentConnection = this;
            _isResponseSent = false;
            _listener = listener;
            _requestStart = DateTime.Now;
            this.socket = s;
            _sbuffer = new StringBuilder();
            _buffer = new byte[_BUFFER_SIZE];
            _headerRecieved = false;
            _requestComplete = false;
            _mreHeader = new ManualResetEvent(false);
            _mreContent = new ManualResetEvent(false);
            _mreHeaderParsed = new ManualResetEvent(false);
            _outStream = new MemoryStream();
            _responseWriter = new StreamWriter(_outStream);
            _responseHeaders = new HeaderCollection();
            _responseHeaders["Server"] = Messages.Current["Org.Reddragonit.EmbeddedWebServer.DefaultHeaders.Server"];
            _responseStatus = HttpStatusCodes.OK;
            _responseCookie = new CookieCollection();
            if (listener.UseSSL)
            {
                inputStream = new SslStream(socket.GetStream(), true);
                ((SslStream)inputStream).AuthenticateAsServer(cert);
            }
            else
                inputStream = socket.GetStream();
            _exit = false;
            _backgroundReaderThread = new Thread(new ThreadStart(backgroundRunStart));
            _backgroundReaderThread.IsBackground = true;
            _backgroundReaderThread.Start();
            try
            {
                Logger.LogMessage(DiagnosticsLevels.TRACE, "Parsing incoming http request [id:" + _id.ToString() + "]");
                parseRequest();
            }
            catch (Exception e)
            {
                this.ResponseStatus = HttpStatusCodes.Bad_Request;
                this.ResponseHeaders.ContentType="text/html";
                this.ResponseWriter.WriteLine(e.Message);
                this.ResponseWriter.Flush();
                this.SendResponse();
                Logger.LogMessage(DiagnosticsLevels.TRACE,"Response sent back due to error in request.");
                Logger.LogError(e);
            }
            Logger.LogMessage(DiagnosticsLevels.TRACE, "Total time to load request: " + DateTime.Now.Subtract(_requestStart).TotalMilliseconds.ToString() + "ms [id:" + _id.ToString() + "]");
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
            _url = new Uri("http://" + _url.Host + site.DefaultPage+_url.Query);
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
                _mreContent.WaitOne();
                if (_requestHeaders.ContentType.StartsWith("multipart/form-data"))
                {
                    if (_requestHeaders.ContentType.Contains("boundary=")||
                        (_requestHeaders.ContentTypeBoundary!=null))
                    {
                        string boundary = (_requestHeaders.ContentTypeBoundary!=null ? _requestHeaders.ContentTypeBoundary : 
                                "--"+_requestHeaders.ContentType.Substring(_requestHeaders.ContentType.IndexOf("boundary=") + "boundary=".Length).Replace("-",""));
                        string line;
                        string var;
                        string value;
                        string fileName;
                        while ((line = streamReadLine(ref _content)) != null)
                        {
                            if (line.EndsWith(boundary + "--"))
                                break;
                            else if (line.EndsWith(boundary))
                            {
                                line = streamReadLine(ref _content);
                                if (line.Contains("filename="))
                                {
                                    var = line.Substring(line.IndexOf("name=\"") + "name=\"".Length);
                                    var = var.Substring(0, var.IndexOf("\";"));
                                    fileName = line.Substring(line.IndexOf("filename=\"") + "filename=\"".Length);
                                    fileName = fileName.Substring(0, fileName.Length - 1);
                                    string contentType = streamReadLine(ref _content);
                                    contentType = contentType.Substring(contentType.IndexOf(":")+1);
                                    contentType=contentType.Trim();
                                    streamReadLine(ref _content);
                                    MemoryStream str = new MemoryStream();
                                    BinaryWriter br = new BinaryWriter(str);
                                    while ((line = PeakLine(_content)) != null)
                                    {
                                        if (line.EndsWith(boundary) || line.EndsWith(boundary + "--"))
                                            break;
                                        else
                                            br.Write(line.ToCharArray());
                                        streamReadLine(ref _content);
                                    }
                                    br.Flush();
                                    str.Seek(0, SeekOrigin.Begin);
                                    uploadedFiles.Add(var, new UploadedFile(var, fileName, contentType, str));
                                }
                                else
                                {
                                    var = line.Substring(line.IndexOf("name=\"") + "name=\"".Length);
                                    var = var.Substring(0, var.Length-1);
                                    streamReadLine(ref _content);
                                    value = "";
                                    while ((line = PeakLine(_content)) != null)
                                    {
                                        if (line.EndsWith(boundary)||line.EndsWith(boundary+"--"))
                                            break;
                                        else
                                            value += streamReadLine(ref _content);
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
                    if (_requestHeaders.CharSet != null)
                        _content = Encoding.GetEncoding(_requestHeaders.CharSet).GetString(ASCIIEncoding.ASCII.GetBytes(_content));
                    string query = HttpUtility.UrlDecode(_content);
                    if (query.StartsWith("?"))
                        query = query.Substring(1);
                    if (query.StartsWith("{") && query.EndsWith("}"))
                    {
                        query = query.Substring(1);
                        if (query != "{}")
                            _jsonParameter = JSON.JsonDecode(query);
                    }
                    else
                    {
                        NameValueCollection col = HttpUtility.ParseQueryString(_content);
                        foreach (string str in col.Keys)
                        {
                            requestParameters.Add(str, HttpUtility.UrlDecode(col[str]));
                        }
                    }
                }
                else if (_requestHeaders.ContentType.StartsWith("application/json"))
                {
                    if (_requestHeaders.CharSet != null)
                        _content = Encoding.GetEncoding(_requestHeaders.CharSet).GetString(ASCIIEncoding.ASCII.GetBytes(_content));
                    string query = HttpUtility.UrlDecode(_content);
                    if (query.StartsWith("?"))
                        query = query.Substring(1);
                    if (query.StartsWith("{") && query.EndsWith("}"))
                    {
                        if (query != "{}")
                            _jsonParameter = JSON.JsonDecode(query);
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
            bool usedBackendProcessing = false;
            Logger.LogMessage(DiagnosticsLevels.TRACE, "Waiting for request data to come in from socket");
            if (!_mreHeader.WaitOne(MAX_HEADER_WAIT_TIME))
            {
                usedBackendProcessing = true;
                _exit = true;
                Logger.LogMessage(DiagnosticsLevels.TRACE, "Timeout reached waiting on request header, attempting failsafe.");
                int b = 0;
                try
                {
                    Logger.LogMessage(DiagnosticsLevels.TRACE, "Attempting to obtain any data remaining on the socket buffer.");
                    b = inputStream.EndRead((IAsyncResult)new object());
                    if (b > 0)
                        _sbuffer.Append(ASCIIEncoding.ASCII.GetString(_buffer, 0, b));
                }
                catch (Exception e) { }
                try
                {
                    b = inputStream.ReadByte();
                    Logger.LogMessage(DiagnosticsLevels.TRACE, "Peaked for data on the socket, returned byte " + b.ToString());
                    if (b != -1)
                    {
                        Logger.LogMessage(DiagnosticsLevels.TRACE, "Appending byte to buffer from peak.");
                        _sbuffer.Append((char)b);
                        inputStream.ReadTimeout = 1000;
                        Logger.LogMessage(DiagnosticsLevels.TRACE, "Attempting to read remainder of data from stream.");
                        b = 1;
                        while (b > 0)
                        {
                            _buffer = new byte[BUF_SIZE];
                            b = inputStream.Read(_buffer, 0, BUF_SIZE);
                            if (b>0)
                                _sbuffer.Append(ASCIIEncoding.ASCII.GetString(_buffer, 0, b));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex);
                }
                if (_sbuffer.Length > 0)
                {
                    Logger.LogMessage(DiagnosticsLevels.TRACE, "Data contained within buffer attempting to process.");
                    if (_sbuffer.ToString().Contains("\r\n\r\n")){
                        Logger.LogMessage(DiagnosticsLevels.TRACE, "Header recieved for connection");
                        _headerRecieved = true;
                        _header = _sbuffer.ToString().Substring(0, _sbuffer.ToString().IndexOf("\r\n\r\n"));
                        _sbuffer.Replace(_header + "\r\n\r\n", "");
                        Logger.LogMessage(DiagnosticsLevels.TRACE, "Connection header received: "+ _header);
                    }
                    if (!_headerRecieved)
                    {
                        Logger.LogMessage(DiagnosticsLevels.TRACE, "Unsure if proper header was recieved, checking for http end code in buffer");
                        if (_sbuffer.ToString().Contains("\r\n"))
                        {
                            Logger.LogMessage(DiagnosticsLevels.TRACE, "Using all buffered data for header.");
                            _headerRecieved = true;
                            _header = _sbuffer.ToString().Trim();
                            _sbuffer = new StringBuilder();
                        }
                        else
                            throw new Exception("No valid HTTP Header was recieved.{" + _sbuffer.ToString() + "}");
                    }
                    else
                    {
                        _exit = false;
                        try
                        {
                            _backgroundReaderThread.Start();
                        }
                        catch (Exception ex) { }
                    }
                }
                else
                    throw new Exception("No valid HTTP Header was recieved.");
                if (_headerRecieved)
                    Logger.LogMessage(DiagnosticsLevels.TRACE, "Attempting to parse HTTP Header: " + _header);
            }
            string request = streamReadLine(ref _header);
            string[] tokens = request.Split(' ');
            if (tokens.Length != 3)
            {
                throw new Exception("invalid http request line");
            }
            _method = tokens[0].ToUpper();
            _version = tokens[2];
            String line;
            _requestHeaders = new HeaderCollection();
            while ((line = streamReadLine(ref _header)) != null)
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
                if (name == "Content-Type")
                {
                    if (value.Contains(";"))
                    {
                        string[] tmp = value.Split(';');
                        value = tmp[0];
                        for (int x = 1; x < tmp.Length; x++)
                        {
                            if (tmp[x].Trim().ToLower().StartsWith("charset"))
                            {
                                _requestHeaders.CharSet = tmp[x].Substring(tmp[x].IndexOf("=") + 1);
                            }
                            else if (tmp[x].Trim().ToLower().StartsWith("boundary"))
                            {
                                _requestHeaders.ContentTypeBoundary = tmp[x].Substring(tmp[x].IndexOf("=") + 1);
                            }
                        }
                    }
                }
                _requestHeaders[name] = value;
            }
            _url = new Uri("http://" + _requestHeaders.Host.Replace("//", "/") + tokens[1].Replace("//", "/"));
            _requestCookie = new CookieCollection(_requestHeaders["Cookie"]);
            if (usedBackendProcessing)
            {
                if (_requestHeaders.ContentLength != null)
                {
                    if (_sbuffer.Length == int.Parse(_requestHeaders.ContentLength))
                    {
                        _content = _sbuffer.ToString().TrimEnd(new char[] { '\r', '\n' });
                        _requestComplete = true;
                        _mreContent.Set();
                    }
                }
                else if (_sbuffer.ToString() == "{}")
                {
                    _content = _sbuffer.ToString().TrimEnd(new char[] { '\r', '\n' });
                    _requestComplete = true;
                    _mreContent.Set();
                }
                if (!_headerRecieved || !_requestComplete)
                {
                    _exit = false;
                    try
                    {
                        _backgroundReaderThread.Start();
                    }
                    catch (Exception ex) { }
                }
            }else
                _mreHeaderParsed.Set();
        }

        private string streamReadLine(ref string stream)
        {
            string ret = null;
            if (stream.Length > 0)
            {
                if (stream.Contains("\r\n"))
                {
                    ret = stream.Substring(0, stream.IndexOf("\r\n"));
                    stream = stream.Substring(stream.IndexOf("\r\n") + 2);
                }
                else
                {
                    ret = stream;
                    stream = "";
                }
            }
            return ret;
        }

        private string PeakLine(string stream)
        {
            string ret = null;
            if (stream.Length > 0)
            {
                if (stream.Contains("\r\n"))
                    ret = stream.Substring(0, stream.IndexOf("\r\n"));
                else
                    ret = stream;
            }
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

        private string CookieDateFormat
        {
            get
            {
                string ret = "r";
                if (RequestHeaders != null)
                {
                    if (RequestHeaders.Browser != null)
                    {
                        switch (RequestHeaders.Browser.BrowserFamily)
                        {
                            case BrowserFamilies.Chrome:
                                ret = "ddd, dd-MMM-yyyy HH:mm:ss 'GMT'";
                                break;
                        }
                    }
                }
                return ret;
            }
        }

        private string CookieFormat
        {
            get
            {
                string ret = "Set-Cookie: {0}={1}; Path={2}; Expires={3};\r\n";
                if (RequestHeaders != null)
                {
                    if (RequestHeaders.Browser != null)
                    {
                        switch (RequestHeaders.Browser.BrowserFamily)
                        {
                            case BrowserFamilies.Chrome:
                                ret = "Set-Cookie: {0}={1}; path={2}; expires={3};\r\n";
                                break;
                        }
                    }
                }
                return ret;
            }
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
            if (!_isResponseSent)
            {
                _isResponseSent = true;
                ResponseWriter.Flush();
                _exit = true;
                DateTime start = DateTime.Now;
                _responseHeaders.ContentLength = _outStream.Length.ToString();
                if (_responseHeaders["Accept-Ranges"] == null)
                    _responseHeaders["Accept-Ranges"] = Messages.Current["Org.Reddragonit.EmbeddedWebServer.DefaultHeaders.AcceptRanges"];
                if (_responseHeaders.ContentType == null)
                    _responseHeaders.ContentType = Messages.Current["Org.Reddragonit.EmbeddedWebServer.DefaultHeaders.ContentType"];
                if (_responseHeaders["Server"] == null)
                    _responseHeaders["Server"] = Messages.Current["Org.Reddragonit.EmbeddedWebServer.DefaultHeaders.Server"];
                if (_responseHeaders.Date == null)
                    _responseHeaders.Date = DateTime.Now.ToString(CookieDateFormat);
                _responseHeaders["Connection"] = "Close";
                Stream outStream;
                try
                {
                    outStream= (_listener.UseSSL ? inputStream : (Stream)socket.GetStream());
                }
                catch (Exception e)
                {
                    Logger.LogError(e);
                    return;
                }
                string line = "HTTP/1.0 " + ((int)ResponseStatus).ToString() + " " + ResponseStatus.ToString().Replace("_", "") + "\r\n";
                foreach (string str in _responseHeaders.Keys)
                    line += str + ": " + _responseHeaders[str] + "\r\n";
                if (_responseCookie != null)
                {
                    if (Site.CurrentSite != null)
                        _responseCookie.Renew(Site.CurrentSite.CookieExpireMinutes);
                    bool setIt = false;
                    if (RequestCookie == null)
                        setIt = true;
                    else if (RequestCookie.Expiry.Subtract(DateTime.Now).TotalMinutes < 5)
                        setIt = true;
                    foreach (string str in _responseCookie.Keys)
                    {
                        if ((setIt)
                            ||((RequestCookie!=null)&&(RequestCookie[str]==null))
                            ||((RequestCookie != null) && (RequestCookie[str] != null) && (RequestCookie[str]!=_responseCookie[str]))
                            )
                            line += string.Format(CookieFormat, new object[] { str, _responseCookie[str], "/", _responseCookie.Expiry.ToUniversalTime().ToString(CookieDateFormat) });
                    }
                }
                line += "\r\n";
                outStream.Write(System.Text.ASCIIEncoding.ASCII.GetBytes(line), 0, line.Length);
                if (this.URL!=null)
                    Logger.LogMessage(DiagnosticsLevels.TRACE, "Time to send headers for URL " + this.URL.AbsolutePath + " = " + DateTime.Now.Subtract(start).TotalMilliseconds.ToString() + "ms");
                start = DateTime.Now;
                byte[] buffer = new byte[socket.Client.SendBufferSize];
                Logger.LogMessage(DiagnosticsLevels.TRACE, "Sending Buffer size: " + socket.Client.SendBufferSize.ToString());
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
                        Logger.LogError(e);
                        _outStream.Position = _outStream.Length;
                    }
                }
                outStream.Flush();
                try
                {
                    socket.Close();
                }
                catch (Exception e) {
                    Logger.LogError(e);
                }
                if (this.URL != null)
                    Logger.LogMessage(DiagnosticsLevels.TRACE, "Time to send response content for URL " + this.URL.AbsolutePath + " = " + DateTime.Now.Subtract(start).TotalMilliseconds.ToString() + "ms");
            }
        }

        //houses the headers used in the response
        private HeaderCollection _responseHeaders;
        public HeaderCollection ResponseHeaders
        {
            get { return _responseHeaders; }
            set { _responseHeaders = value; }
        }

        //houses the response status
        private HttpStatusCodes _responseStatus = HttpStatusCodes.OK;
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
