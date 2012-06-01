using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Org.Reddragonit.EmbeddedWebServer.Diagnostics;
using Org.Reddragonit.EmbeddedWebServer.Interfaces;

namespace Org.Reddragonit.EmbeddedWebServer.Components.Message
{
    internal class HttpResponse : IDisposable
    {
        private HttpRequest _request;

        internal HttpResponse(HttpRequest request)
        {
            _request = request;
            _outStream = new MemoryStream();
            _responseWriter = new StreamWriter(_outStream);
            _responseHeaders = new HeaderCollection();
            _responseHeaders["Server"] = Messages.Current["Org.Reddragonit.EmbeddedWebServer.DefaultHeaders.Server"];
            _responseStatus = HttpStatusCodes.OK;
            _responseCookie = new CookieCollection();
            _isResponseSent = false;
        }

        private HttpResponse() { }

        //holds whether or not the response was already send
        private bool _isResponseSent;
        public bool IsResponseSent
        {
            get { return _isResponseSent; }
        }

        //houses the outgoing buffer to write out information
        private Stream _outStream;

        //used to access the response buffer in order to write the response to it
        private StreamWriter _responseWriter;
        public StreamWriter ResponseWriter
        {
            get { return _responseWriter; }
        }

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
                if (_request.Headers != null)
                {
                    if (_request.Headers.Browser != null)
                    {
                        switch (_request.Headers.Browser.BrowserFamily)
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
                if (_request.Headers != null)
                {
                    if (_request.Headers.Browser != null)
                    {
                        switch (_request.Headers.Browser.BrowserFamily)
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
                if (_request.Headers["Connection"] != null)
                {
                    if (_request.Headers["Connection"].ToLower() == "keep-alive")
                        _responseHeaders["Connection"] = _request.Headers["Connection"];
                    else
                        _responseHeaders["Connection"] = "close";
                }
                else
                    _responseHeaders["Connection"] = "close";
                MemoryStream outStream = new MemoryStream();
                string line = "HTTP/1.0 " + ((int)ResponseStatus).ToString() + " " + ResponseStatus.ToString().Replace("_", "") + "\r\n";
                foreach (string str in _responseHeaders.Keys)
                    line += str + ": " + _responseHeaders[str] + "\r\n";
                if (_responseCookie != null)
                {
                    if (Site.CurrentSite != null)
                        _responseCookie.Renew(Site.CurrentSite.CookieExpireMinutes);
                    bool setIt = false;
                    if (_request.Cookie == null)
                        setIt = true;
                    else if (_request.Cookie.Expiry.Subtract(DateTime.Now).TotalMinutes < 5)
                        setIt = true;
                    foreach (string str in _responseCookie.Keys)
                    {
                        if ((setIt)
                            || ((_request.Cookie != null) && (_request.Cookie[str] == null))
                            || ((_request.Cookie != null) && (_request.Cookie[str] != null) && (_request.Cookie[str] != _responseCookie[str]))
                            )
                            line += string.Format(CookieFormat, new object[] { str, _responseCookie[str], "/", _responseCookie.Expiry.ToUniversalTime().ToString(CookieDateFormat) });
                    }
                }
                line += "\r\n";
                outStream.Write(System.Text.ASCIIEncoding.ASCII.GetBytes(line), 0, line.Length);
                byte[] buffer = new byte[1024];
                _outStream.Seek(0, SeekOrigin.Begin);
                while (_outStream.Position < _outStream.Length)
                {
                    int len;
                    if (_outStream.Length - _outStream.Position > 1024)
                        len = _outStream.Read(buffer, 0, 1024);
                    else
                        len = _outStream.Read(buffer, 0, (int)(_outStream.Length - _outStream.Position));
                    outStream.Write(buffer, 0, len);
                }
                if (_request.URL != null)
                    Logger.LogMessage(DiagnosticsLevels.TRACE, "Time to send headers for URL " + _request.URL.AbsolutePath + " = " + DateTime.Now.Subtract(start).TotalMilliseconds.ToString() + "ms");
                start = DateTime.Now;
                _request.Connection.SendBuffer(outStream.ToArray(),_responseHeaders["Connection"]=="close");
                if (_request.URL != null)
                    Logger.LogMessage(DiagnosticsLevels.TRACE, "Time to send response content for URL " + _request.URL.AbsolutePath + " = " + DateTime.Now.Subtract(start).TotalMilliseconds.ToString() + "ms");
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

        #region IDisposable Members

        public void Dispose()
        {
            try
            {
                _outStream.Dispose();
            }
            catch (Exception e) { }
        }

        #endregion
    }
}
