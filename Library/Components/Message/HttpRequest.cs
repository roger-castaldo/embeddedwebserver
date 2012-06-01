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
    public class HttpRequest : IDisposable
    {
        private const int _REQUEST_HEADER_TIMEOUT = 2000;

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
            _timer.Change(site.RequestTimeout, Timeout.Infinite);
            _requestTimeout = _requestStart.AddMilliseconds(site.RequestTimeout);
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

        private Timer _timer;

        internal HttpRequest(long id,string[] words,HttpConnection connection,ref HttpParser parser)
        {
            _method = words[0].ToUpper();
            _path = words[1];
            _version = words[2];
            _mreParameters = new ManualResetEvent(false);
            _connection = connection;
            _contentBuffer = new MemoryStream();
            _requestTimeout = _requestStart.AddMilliseconds(int.MaxValue);
            _headers = new HeaderCollection();
            _requestStart = DateTime.Now;
            _timer = new Timer(new TimerCallback(_RequestHeaderTimeout), null, _REQUEST_HEADER_TIMEOUT, Timeout.Infinite);
            parser.RequestHeaderLineRecieved = _RequestHeaderLineReceived;
            parser.RequestHeaderComplete = _RequestHeaderComplete;
            parser.RequestBodyBytesRecieved = _RequestBodyBytesReceived;
            parser.RequestComplete = _RequestComplete;
            _response = new HttpResponse(this);
        }

        private HttpRequest() { }

        #region Parser

        private void _RequestHeaderTimeout(object state)
        {
            _currentRequest = this;
            HttpConnection.SetCurrentConnection(_connection);
            Logger.Trace("Failed to recieve the complete request header prior to the timeout.");
            _connection.SendBuffer(Encoding.Default.GetBytes("HTTP/1.0 " + ((int)HttpStatusCode.BadRequest).ToString() + " Request Header not recieved in a proper amount of time."),true);
        }

        private void _RequestHeaderLineReceived(string name, string value)
        {
            _headers[name] = (value == string.Empty ? null : value);
        }

        private void _RequestHeaderComplete()
        {
            _timer.Dispose();
            _timer = new Timer(new TimerCallback(_RequestTimeout), null, int.MaxValue, Timeout.Infinite);
            _url = new Uri("http://" + _headers.Host.Replace("//", "/") + _path.Replace("//", "/"));
            _cookie = new CookieCollection(_headers["Cookie"]);
            Logger.Trace("Total time to load request: " + DateTime.Now.Subtract(_requestStart).TotalMilliseconds.ToString() + "ms [id:" + _id.ToString() + "]");
            _handlingThread = new Thread(new ThreadStart(_HandleRequest));
            _handlingThread.IsBackground = true;
            _handlingThread.Start();
        }

        private void _RequestTimeout(object state)
        {
            try
            {
                _handlingThread.Abort();
            }
            catch (Exception e) { }
        }

        private void _HandleRequest()
        {
            _currentRequest = this;
            if (this.Headers["expect"]!=null && this.Headers["expect"].ToLower().Contains("100-continue"))
            {
                Logger.Trace("Got 100 continue request.");
                ResponseStatus = HttpStatusCodes.Continue;
                ClearResponse();
                SendResponse();
                _timer.Dispose();
            }else
                _connection.Listener.HandleRequest(this);
        }

        private void _RequestBodyBytesReceived(byte[] buffer, int index, int count)
        {
            _contentBuffer.Write(buffer, index, count);
        }

        private void _RequestComplete()
        {
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
                        query = query.Substring(1);
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
            _url = new Uri("http://" + _url.Host + site.DefaultPage + _url.Query);
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
            get { return _response.IsResponseSent; }
        }
        
        public StreamWriter ResponseWriter
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
            _response.SendResponse();
            _timer.Dispose();
            HttpRequest._currentRequest = null;
            _connection.DisposeRequest(this);
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

        #region IDisposable Members

        public void Dispose()
        {
            try
            {
                _timer.Dispose();
            }
            catch (Exception e) { }
            try
            {
                _response.Dispose();
            }
            catch (Exception e) { }
        }

        #endregion
    }
}
