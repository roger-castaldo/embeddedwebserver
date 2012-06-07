using System;
using System.Collections.Generic;
using System.Text;
using Org.Reddragonit.EmbeddedWebServer.Interfaces;
using System.Net;
using System.Net.Sockets;
using Org.Reddragonit.EmbeddedWebServer.Sessions;
using Org.Reddragonit.EmbeddedWebServer.Diagnostics;
using System.Threading;
using Org.Reddragonit.EmbeddedWebServer.Components.Message;
using Org.Reddragonit.EmbeddedWebServer.Components.MonoFix;

namespace Org.Reddragonit.EmbeddedWebServer.Components
{
    /*
     * This class is an implemented socket that contains the sites it listens for 
     * on the given tcp listener.  It is where the http connections are created 
     * and responses are started.
     */
    internal class PortListener
    {
        private static readonly ObjectPool<HttpConnection> ConnectionPool = new ObjectPool<HttpConnection>(() => new HttpConnection());

        //the tcp connection listener for the given sites
        private WrappedTcpListener _listener;
        //maximum idle time between requests in seconds
        private long _idleSeonds = long.MaxValue;
        //maximum time before refreshing listener in seconds
        private long _totalRunSeconds = long.MaxValue;
        //last connection request occured
        private DateTime _lastConnectionRequest;
        //last connection refreshing
        private DateTime _lastConnectionRefresh;
        //backlog amount
        private int _backLog = 1000;
        //houses the http connections that are running in the background, in a way that they can be killed
        private List<HttpConnection> _currentConnections;
        private bool _shutdown;
        private IAsyncResult _acceptResult;

        private MT19937 _rand;

        //indicate if the connection uses ssl
        private bool _useSSL;
        public bool UseSSL
        {
            get { return _useSSL; }
        }

        //the sites that the listener listens for
        private List<Site> _sites;
        public List<Site> Sites
        {
            get { return _sites; }
        }

        //returns the default site for this tcp listener
        private Site _defaultSite
        {
            get
            {
                foreach (Site s in _sites)
                {
                    if (s.ServerName == null)
                        return s;
                }
                return _sites[0];
            }
        }

        //adds a site to listen for
        public void AttachSite(Site site,sIPPortPair ipp)
        {
            if (UseSSL || ipp.UseSSL)
                new BoundMultipleSSLException(new sIPPortPair(_ip, _port, false));
            if ((ipp.Address == IPAddress.Any)||((IP!=IPAddress.Any)&&(ipp.Address!=_ip)))
                _ip = IPAddress.Any;
            _sites.Add(site);
        }

        //the port the tcplistener is bound to
        private int _port;
        public int Port
        {
            get { return _port; }
        }

        //the ip address the tcplistener is bound on
        private IPAddress _ip;
        public IPAddress IP
        {
            get { return _ip; }
        }

        //creates a new instance of a tcp port listener for a given site
        public PortListener(Site site,sIPPortPair ipp)
        {
            _rand = new MT19937(DateTime.Now.Ticks);
            _sites = new List<Site>();
            _sites.Add(site);
            _port = ipp.Port;
            _ip = ipp.Address;
            _useSSL = ipp.UseSSL;
            _idleSeonds = (long)Math.Min(_idleSeonds, ipp.IdleSeconds);
            _totalRunSeconds = (long)Math.Min(_totalRunSeconds, ipp.TotalRunSeconds);
            _backLog = (int)Math.Min(_backLog, ipp.BackLog);
        }

        //starts the listener by starting each site,
        //then binding the tcplistener to wait for incoming connections
        public void Start()
        {
            _shutdown = false;
            _currentConnections = new List<HttpConnection>();
            foreach (Site site in _sites)
                site.Start();
            _listener = new WrappedTcpListener(new TcpListener(_ip, _port));
            Logger.LogMessage(DiagnosticsLevels.TRACE, "Creating port listener on " + _ip.ToString() + ":" + _port.ToString());
            _listener.Start(_backLog);
            _lastConnectionRefresh = DateTime.Now;
            _lastConnectionRequest = DateTime.Now;
            _acceptResult=_listener.BeginAcceptSocket(new AsyncCallback(RecieveClient), null);
        }

        //stops the tcplistener from accepting connections and stops all sites contained within
        public void Stop()
        {
            _shutdown = true;
            try
            {
                _listener.EndAcceptSocket(null);
            }
            catch (Exception e) {
                Logger.LogError(e);
            }
            _listener.Stop();
            lock (_currentConnections)
            {
                while (_currentConnections.Count > 0)
                {
                    _currentConnections[0].Close();
                    _currentConnections[0].Reset();
                    ConnectionPool.Enqueue(_currentConnections[0]);
                    _currentConnections.RemoveAt(0);
                }
            }
            foreach (Site site in _sites)
                site.Stop();
        }

        /*
         * This function is the asynchronously called function from the being accept on 
         * the tcp listener.  It accepts the tcp client, starts listening again, 
         * and then proceeds to process the client itself by producing an HTTPConnection 
         * from the given tcp client, and then searches through the sites to find the appropriate 
         * site for the given client request.  If no site is found, it simply assumes the default site.
         * Then the site's process request function is called to handle the reuqest from there.  It also 
         * checks that the given site allows for the method specified, if not returns an invalid request 
         * response.
         */
        private void RecieveClient(IAsyncResult res)
        {
            Socket sock = null;
            lock (_listener)
            {
                _lastConnectionRequest = DateTime.Now;
            }
            try
            {
                sock = _listener.EndAcceptSocket(res);
            }
            catch (Exception e) {
                Logger.LogError(e);
                sock = null;
            }
            if (!_shutdown)
            {
                try
                {
                    _acceptResult = _listener.BeginAcceptSocket(new AsyncCallback(RecieveClient), null);
                }
                catch (Exception e)
                {
                    Logger.LogError(e);
                }
            }
            if (sock != null && !_shutdown)
            {
                long id = _rand.NextLong();
                Logger.LogMessage(DiagnosticsLevels.TRACE, "New tcp client recieved, generating http connection [id:" + id.ToString() + "]");
                HttpConnection con = ConnectionPool.Dequeue();
                con.Start(sock, this, (UseSSL ? _sites[0].GetCertificateForEndpoint(new sIPPortPair(_ip, _port, UseSSL)) : null), id);
                lock (_currentConnections)
                {
                    _currentConnections.Add(con);
                }
            }
            else if (_shutdown)
            {
                sock.Disconnect(false);
                sock.Close();
            }

        }

        internal void HandleRequest(HttpRequest request)
        {
            if (!request.IsResponseSent)
            {
                HttpRequest.SetCurrentRequest(request);
                Logger.LogMessage(DiagnosticsLevels.TRACE, "Attempting to process connection request.");
                if (request.URL.AbsolutePath == "/jquery.js")
                {
                    request.ResponseStatus = HttpStatusCodes.OK;
                    request.ResponseHeaders.ContentType = "text/javascript";
                    if (!request.Headers.Browser.IsMobile)
                        request.ResponseWriter.Write(Utility.ReadEmbeddedResource("Org.Reddragonit.EmbeddedWebServer.resources.jquery.min.js"));
                    else
                        request.ResponseWriter.Write(Utility.ReadEmbeddedResource("Org.Reddragonit.EmbeddedWebServer.resources.jquery.mobile.min.js"));
                    request.SendResponse();
                }
                else if (request.URL.AbsolutePath == "/json.js")
                {
                    request.ResponseStatus = HttpStatusCodes.OK;
                    request.ResponseHeaders.ContentType = "text/javascript";
                    request.ResponseWriter.Write(Utility.ReadEmbeddedResource("Org.Reddragonit.EmbeddedWebServer.resources.json2.min.js"));
                    request.SendResponse();
                }
                else
                {
                    DateTime start = DateTime.Now;
                    Site site = null;
                    if (_sites.Count > 1)
                    {
                        foreach (Site s in _sites)
                        {
                            if ((s.ServerName != null) && (s.ServerName == request.URL.Host))
                            {
                                site = s;
                                break;
                            }
                        }
                        if (site==null)
                        {
                            foreach (Site s in _sites)
                            {
                                if (s.Aliases != null)
                                {
                                    foreach (string str in s.Aliases)
                                    {
                                        if (str == request.URL.Host)
                                        {
                                            site = s;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        if (site==null)
                        {
                            foreach (Site s in _sites)
                            {
                                foreach (sIPPortPair ipp in s.ListenOn)
                                {
                                    if ((ipp.Address != IPAddress.Any) && (request.Connection.LocalEndPoint == new IPEndPoint(ipp.Address, ipp.Port)))
                                    {
                                        site = s;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    if (site == null)
                        site = _defaultSite;
                    Logger.LogMessage(DiagnosticsLevels.DEBUG, "Total time to find site: " + DateTime.Now.Subtract(start).TotalMilliseconds.ToString() + "ms");
                    if ((!site.AllowGET && request.Method.ToUpper() == "GET") ||
                        (!site.AllowPOST && request.Method.ToUpper() == "POST"))
                    {
                        request.ResponseStatus = HttpStatusCodes.Method_Not_Allowed;
                        request.SendResponse();
                    }
                    else
                    {
                        request.SetTimeout(site);
                        try
                        {
                            if (request.URL.AbsolutePath == "/")
                                request.UseDefaultPath(site);
                            site.ProcessRequest(request);
                        }
                        catch (ThreadAbortException tae)
                        {
                            if (!request.IsResponseSent)
                            {
                                request.ResponseStatus = HttpStatusCodes.Request_Timeout;
                                request.ClearResponse();
                                request.ResponseWriter.WriteLine("The server timed out processing the request.");
                                request.SendResponse();
                            }
                        }
                        catch (Exception e)
                        {
                            Logger.LogError(e);
                            if (!request.IsResponseSent)
                            {
                                request.ResponseStatus = HttpStatusCodes.Internal_Server_Error;
                                request.ClearResponse();
                                request.ResponseWriter.Write(e.Message);
                                request.SendResponse();
                            }
                        }
                        Logger.LogMessage(DiagnosticsLevels.DEBUG, "Total time to process request to URL " + request.URL.AbsolutePath + " = " + DateTime.Now.Subtract(start).TotalMilliseconds.ToString() + "ms");
                    }
                }
            }
            else
                Logger.LogMessage(DiagnosticsLevels.TRACE, "Response sent prior to processing, error in request.");
        }

        internal void CheckRefresh()
        {
            lock (_listener)
            {
                if (
                        (DateTime.Now.Subtract(_lastConnectionRequest).TotalSeconds>_idleSeonds)||
                        (DateTime.Now.Subtract(_lastConnectionRefresh).TotalSeconds > _totalRunSeconds)
                    )
                try
                {
                    _listener.EndAcceptSocket(_acceptResult);
                }
                catch (Exception e)
                {
                    Logger.LogError(e);
                }
                _lastConnectionRefresh = DateTime.Now;
                _lastConnectionRequest = DateTime.Now;
                _acceptResult = _listener.BeginAcceptSocket(new AsyncCallback(RecieveClient), null);
            }
        }

        internal void ClearConnection(HttpConnection httpConnection)
        {
            if (!_shutdown)
            {
                lock (_currentConnections)
                {
                    for (int x = 0; x < _currentConnections.Count; x++)
                    {
                        if (httpConnection.ID == _currentConnections[x].ID)
                        {
                            _currentConnections.RemoveAt(x);
                            httpConnection.Reset();
                            ConnectionPool.Enqueue(httpConnection);
                        }
                    }
                }
            }
        }
    }
}
