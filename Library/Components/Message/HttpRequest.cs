using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.IO;
using System.Threading;
using Org.Reddragonit.EmbeddedWebServer.Interfaces;
using Org.Reddragonit.EmbeddedWebServer.Diagnostics;
using Org.Reddragonit.EmbeddedWebServer.Sessions;
using Procurios.Public;
using System.Collections.Specialized;

namespace Org.Reddragonit.EmbeddedWebServer.Components.Message
{
    public class HttpRequest
    {
        private const int _DEFAULT_REQUEST_TIMEOUT = 120000;
        [ThreadStatic()]
        private static HttpRequest _currentRequest;
        public static HttpRequest CurrentRequest
        {
            get { return _currentRequest; }
        }
        internal static void SetCurrentRequest(HttpRequest req)
        {
            _currentRequest = req;
        }

        public bool IsMobile
        {
            get { return (Headers.Browser != null ? Headers.Browser.IsMobile : false); }
        }

        private Thread _handlingThread;
        //used to buffer Body data
        private MemoryStream _contentBuffer;
        //used to house the data returned on an async read from the input stream
        private ManualResetEvent _mreParameters;
        //used to parse the http data
        private HttpParser _parser;
        private HttpConnection _connection;
        internal HttpConnection Connection
        {
            get { return _connection; }
        }

        public EndPoint Client
        {
            get { return _connection.Client; }
        }

        private string _path;

        private DateTime _requestTimeout;
        internal void SetTimeout(Site site)
        {
            _connection.ClearTimer();
            if (_timer!=null)
                _timer.Change(site.RequestTimeout, Timeout.Infinite);
            _requestTimeout = _requestStart.AddMilliseconds(site.RequestTimeout);
        }

        public void SetTimeout(int milliseconds)
        {
            _timer.Change(milliseconds, Timeout.Infinite);
            _requestTimeout = _requestStart.AddMilliseconds(milliseconds);
        }

        internal DateTime TimeoutTime
        {
            get { return _requestTimeout; }
        }

        internal bool TimedOut
        {
            get { return DateTime.Now.Ticks < _requestTimeout.Ticks; }
        }

        private HttpResponse _response;
        private long _id;
        public long ID{
            get{return _id;}
        }


        public bool IsSSL
        {
            get { return _connection.Listener.UseSSL; }
        }

        private Timer _timer;

        internal void Reset()
        {
            if (_timer != null)
            {
                _timer.Dispose();
                _timer = null;
            }
            _method = null;
            _path = null;
            _version = null;
            _mreParameters.Reset();
            _connection = null;
            _contentBuffer = new MemoryStream();
            _headers = null;
            _parameters = null;
            _jsonParameter = null;
            _uploadedFiles = null;
            _handlingThread = null;
            _response.Dispose();
            _response = null;
        }

        internal HttpRequest() { }

        internal void StartRequest(long id,string[] words,HttpConnection connection,ref HttpParser parser)
        {
            _id = id;
            _method = words[0].ToUpper();
            _path = words[1];
            _version = words[2];
            _mreParameters = new ManualResetEvent(false);
            _connection = connection;
            _contentBuffer = new MemoryStream();
            _requestTimeout = _requestStart.AddMilliseconds(int.MaxValue);
            _headers = new HeaderCollection();
            _requestStart = DateTime.Now;
            _parser = parser;
            _response = new HttpResponse(this);
            parser.RequestHeaderLineRecieved = _RequestHeaderLineReceived;
            parser.RequestHeaderComplete = _RequestHeaderComplete;
            parser.RequestBodyBytesRecieved = _RequestBodyBytesReceived;
            parser.RequestComplete = _RequestComplete;
        }

        #region Parser

        private void _RequestHeaderLineReceived(string name, string value)
        {
            _connection.ResetTimer();
            Logger.Trace("Header Line: Headers[" + (_headers == null ? "NULL" : "NOT_NULL") + "],name[" + (name == null ? "NULL" : "NOT_NULL") + "],value[" + (value == null ? "NULL" : "NOT_NULL") + "]");
            _headers[name] = (value == string.Empty ? null : value);
        }

        private void _RequestHeaderComplete(bool hasBody,bool bodyComplete,out bool callComplete)
        {
            callComplete = hasBody;
            _connection.HeaderComplete();
            _parser.RequestHeaderLineRecieved = null;
            _parser.RequestHeaderComplete = null;
            _timer = new Timer(new TimerCallback(_RequestTimeout), null, _DEFAULT_REQUEST_TIMEOUT, Timeout.Infinite);
            _url = new Uri("http://" + _headers.Host.Replace("//", "/") + _path.Replace("//", "/"));
            _cookie = new CookieCollection(_headers["Cookie"]);
            if (!hasBody)
                _RequestComplete();
            else if (bodyComplete)
                _ParseBody();
            Logger.Trace("Total time to load request: " + DateTime.Now.Subtract(_requestStart).TotalMilliseconds.ToString() + "ms [id:" + _id.ToString() + "]");
            _handlingThread = Thread.CurrentThread;
            _currentRequest = this;
            if (this.Headers != null)
            {
                if (this.Headers["expect"] != null)
                {
                    if (this.Headers["expect"].ToLower().Contains("100-continue"))
                    {
                        Logger.Trace("Got 100 continue request.");
                        ResponseStatus = HttpStatusCodes.Continue;
                        ClearResponse();
                        SendResponse();
                        _timer.Dispose();
                    }
                    else
                        _connection.Listener.HandleRequest(this);
                }
                else
                    _connection.Listener.HandleRequest(this);
            }
        }

        private void _RequestTimeout(object state)
        {
            _parser.RequestBodyBytesRecieved = null;
            _parser.RequestComplete = null;
            if (!IsResponseSent)
            {
                ResponseStatus = HttpStatusCodes.Request_Timeout;
                ClearResponse();
                ResponseWriter.WriteLine("The server timed out processing the request.");
                SendResponse();
            }
            try
            {
                _handlingThread.Abort();
            }
            catch (Exception e) { }
        }

        private void _RequestBodyBytesReceived(byte[] buffer, int index, int count)
        {
            _contentBuffer.Write(buffer, index, count);
        }

        private void _RequestComplete()
        {
            _parser.RequestBodyBytesRecieved = null;
            _parser.RequestComplete = null;
            if (_parameters == null)
                _ParseBody();
        }

        private void _ParseBody(){
            Dictionary<string, string> Parameters = new Dictionary<string, string>();
            Dictionary<string, UploadedFile> uploadedFiles = new Dictionary<string, UploadedFile>();
            if (URL.Query != null)
            {
                string query = HttpUtility.UrlDecode(URL.Query);
                if (query.StartsWith("?{") && query.EndsWith("}"))
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
                        Parameters.Add(str, col[str]);
                    }
                }
            }
            if (_contentBuffer.Length>0)
            {
                string _content = Encoding.Default.GetString(_contentBuffer.ToArray());
                if (_headers.ContentType.StartsWith("multipart/form-data"))
                {
                    if (_headers.ContentType.Contains("boundary=") ||
                        (_headers.ContentTypeBoundary != null))
                    {
                        string boundary = (_headers.ContentTypeBoundary != null ? _headers.ContentTypeBoundary :
                                "--" + _headers.ContentType.Substring(_headers.ContentType.IndexOf("boundary=") + "boundary=".Length).Replace("-", ""));
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
                                    contentType = contentType.Substring(contentType.IndexOf(":") + 1);
                                    contentType = contentType.Trim();
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
                                    var = var.Substring(0, var.Length - 1);
                                    streamReadLine(ref _content);
                                    value = "";
                                    while ((line = PeakLine(_content)) != null)
                                    {
                                        if (line.EndsWith(boundary) || line.EndsWith(boundary + "--"))
                                            break;
                                        else
                                            value += streamReadLine(ref _content);
                                    }
                                    Parameters.Add(var, value.Trim());
                                }
                            }
                        }
                    }
                    else
                        throw new Exception("Unknown format, content-type: " + _headers.ContentType + " unable to parse in parameters.");
                }
                else if (_headers.ContentType == "application/x-www-form-urlencoded")
                {
                    if (_headers.CharSet != null)
                        _content = Encoding.GetEncoding(_headers.CharSet).GetString(ASCIIEncoding.ASCII.GetBytes(_content));
                    string query = HttpUtility.UrlDecode(_content);
                    if (query.StartsWith("?"))
                        query = query.Substring(1);
                    if (query.StartsWith("{") && query.EndsWith("}"))
                    {
                        query = (query.StartsWith("{{") ? query.Substring(1) : query);
                        if (query != "{}")
                            _jsonParameter = JSON.JsonDecode(query);
                    }
                    else
                    {
                        NameValueCollection col = HttpUtility.ParseQueryString(_content);
                        foreach (string str in col.Keys)
                        {
                            Parameters.Add(str, HttpUtility.UrlDecode(col[str]));
                        }
                    }
                }
                else if (_headers.ContentType.StartsWith("application/json"))
                {
                    if (_headers.CharSet != null)
                        _content = Encoding.GetEncoding(_headers.CharSet).GetString(ASCIIEncoding.ASCII.GetBytes(_content));
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
                    throw new Exception("Unknown format, content-type: " + _headers.ContentType + " unable to parse in parameters.");
            }
            _parameters = new ParameterCollection(Parameters);
            _uploadedFiles = new UploadedFileCollection(uploadedFiles);
            _mreParameters.Set();
        }
        #endregion

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
            get
            {
                if (_contextVariables != null)
                {
                    if (_contextVariables.ContainsKey(ConnectionVariableName))
                        return _contextVariables[ConnectionVariableName];
                }
                return null;
            }
            set
            {
                if (_contextVariables == null)
                    _contextVariables = new Dictionary<string, object>();
                if (_contextVariables.ContainsKey(ConnectionVariableName))
                    _contextVariables.Remove(ConnectionVariableName);
                if (value != null)
                    _contextVariables.Add(ConnectionVariableName, value);
            }
        }

        private DateTime _requestStart;
        public DateTime RequestStart
        {
            get { return _requestStart; }
        }

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
            _url = new Uri("http://" + _url.Host + site.DefaultPage(IsMobile) + _url.Query);
        }

        //returns the http version specified in the request
        private string _version;
        public string Version
        {
            get { return _version; }
        }

        //houses the headers specified in the request
        private HeaderCollection _headers;
        public HeaderCollection Headers
        {
            get { return _headers; }
        }

        //houses the parameters specified in the request.  They are loaded at the point
        //that this property is called the first time to be more efficient
        private ParameterCollection _parameters;
        public ParameterCollection Parameters
        {
            get
            {
                _mreParameters.WaitOne();
                _mreParameters.Set();
                return _parameters;
            }
        }

        //houses all the uploaded file information from the request.  These are loaded 
        //at the point that this property is called the first time, or when the 
        //request parameters are loaded the first time.
        private UploadedFileCollection _uploadedFiles;
        public UploadedFileCollection UploadedFiles
        {
            get
            {
                _mreParameters.WaitOne();
                _mreParameters.Set(); 
                return _uploadedFiles;
            }
        }

        //houses the cookie information specified by the request this is loaded with the headers
        private CookieCollection _cookie;
        public CookieCollection Cookie
        {
            get { return _cookie; }
        }

        //houses the json object specified in the query, typically used by the embedded services
        private object _jsonParameter = null;
        public object JSONParameter
        {
            get
            {
                _mreParameters.WaitOne();
                _mreParameters.Set(); 
                return _jsonParameter;
            }
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

        #region Response
        public bool IsResponseSent
        {
            get {
                if (_response == null)
                    return true;
                return _response.IsResponseSent; 
            }
        }
        
        public HttpStreamWriter ResponseWriter
        {
            get { return _response.ResponseWriter; }
        }

        //called to override the response buffer stream with an internally created one
        public void UseResponseStream(Stream str)
        {
            _response.UseResponseStream(str);
        }

        //clears all response data and produces a new response buffer
        public void ClearResponse()
        {
            _response.ClearResponse();
        }

        public void SendResponse()
        {
            if (_timer != null)
            {
                _timer.Dispose();
                _timer = null;
            }
            try
            {
                _response.SendResponse();
            }
            catch (Exception e) { }
            HttpRequest._currentRequest = null;
            if (_connection!=null)
                _connection.CompleteRequest(this);
        }

        public HeaderCollection ResponseHeaders
        {
            get { return _response.ResponseHeaders; }
            set { _response.ResponseHeaders = value; }
        }

        public HttpStatusCodes ResponseStatus
        {
            get { return _response.ResponseStatus; }
            set { _response.ResponseStatus = value; }
        }

        public CookieCollection ResponseCookie
        {
            get { return _response.ResponseCookie; }
            set { _response.ResponseCookie = value; }
        }
        #endregion

        internal void Dispose()
        {
            if (_timer != null)
            {
                _timer.Dispose();
                _timer = null;
            }
            _contentBuffer.Dispose();
            _response.Dispose();
        }
    }
}
