using System;
using System.Collections.Generic;
using System.Text;
using Org.Reddragonit.EmbeddedWebServer.Interfaces;
using System.Net;
using System.Net.Sockets;
using Org.Reddragonit.EmbeddedWebServer.Sessions;
using Org.Reddragonit.EmbeddedWebServer.Diagnostics;
using System.Threading;

namespace Org.Reddragonit.EmbeddedWebServer.Components
{
    /*
     * This class is an implemented socket that contains the sites it listens for 
     * on the given tcp listener.  It is where the http connections are created 
     * and responses are started.
     */
    internal class PortListener
    {
        //the tcp connection listener for the given sites
        private TcpListener _listener;
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

        private Thread _pollingThread = null;
        private bool _abort = false;
        private const int _THREAD_SLEEP_MS = 100;
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
            foreach (Site site in _sites)
                site.Start();
            _listener = new TcpListener(_ip, _port);
            Logger.LogMessage(DiagnosticsLevels.TRACE, "Creating port listener on " + _ip.ToString() + ":" + _port.ToString());
            _listener.Start(_backLog);
            _lastConnectionRefresh = DateTime.Now;
            _lastConnectionRequest = DateTime.Now;
            //patch to handle running on linux as it appears that async times out
            if (Utility.IsLinux)
            {
                _abort = false;
                _pollingThread = new Thread(new ThreadStart(_startPoller));
                _pollingThread.Name = "HttpPortRequestPollingThread";
                _pollingThread.Start();
            }
            else
                _listener.BeginAcceptTcpClient(new AsyncCallback(RecieveClient), null);
        }

        private void _startPoller()
        {
            while (!_abort)
            {
                try
                {
                    while (_listener.Pending() && !_abort)
                    {
                        Logger.LogMessage(DiagnosticsLevels.TRACE, "New http tcp client connection request recieved");
                        new Thread(new ParameterizedThreadStart(_LoadClient)).Start(_listener.AcceptTcpClient());
                        Logger.LogMessage(DiagnosticsLevels.TRACE, "New http tcp client connection request processed");
                    }
                }
                catch (Exception e)
                {
                    Logger.LogError(e);
                }
                if (!_abort)
                    Thread.Sleep(_THREAD_SLEEP_MS);
            }
        }

        //stops the tcplistener from accepting connections and stops all sites contained within
        public void Stop()
        {
            _abort = true;
            try
            {
                _listener.EndAcceptTcpClient(null);
            }
            catch (Exception e) {
                Logger.LogError(e);
            }
            _listener.Stop();
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
            TcpClient clnt = null;
            lock (_listener)
            {
                _lastConnectionRequest = DateTime.Now;
            }
            try
            {
                clnt = _listener.EndAcceptTcpClient(res);
            }
            catch (Exception e) {
                Logger.LogError(e);
                clnt = null;
            }
            try
            {
                _listener.BeginAcceptTcpClient(new AsyncCallback(RecieveClient), null);
            }
            catch (Exception e) {
                Logger.LogError(e);
            }
            _LoadClient(clnt);
        }

        private void _LoadClient(object obj)
        {
            TcpClient clnt = null;
            if (obj != null)
                clnt = (TcpClient)obj;
            if (clnt != null)
            {
                long id = _rand.NextLong();
                Logger.LogMessage(DiagnosticsLevels.TRACE, "New tcp client recieved, generating http connection [id:"+id.ToString()+"]");
                HttpConnection con = null;
                try
                {
                    con = (UseSSL ? new HttpConnection(clnt, new sIPPortPair(_ip, _port, UseSSL), _sites[0].GetCertificateForEndpoint(new sIPPortPair(_ip, _port, UseSSL)),id)
                        : new HttpConnection(clnt, new sIPPortPair(_ip, _port, UseSSL), null,id));
                }
                catch (Exception e)
                {
                    Logger.LogError(e);
                    return;
                }
                Logger.LogMessage(DiagnosticsLevels.TRACE, "Setting current Http Connection [id:" + id.ToString() + "]");
                if (!con.IsResponseSent)
                {
                    if (con.URL.AbsolutePath == "/jquery.js")
                    {
                        con.ResponseStatus = HttpStatusCodes.OK;
                        con.ResponseHeaders.ContentType = "text/javascript";
                        if (!con.RequestHeaders.Browser.IsMobile)
                            con.ResponseWriter.Write(Utility.ReadEmbeddedResource("Org.Reddragonit.EmbeddedWebServer.resources.jquery.min.js"));
                        else
                            con.ResponseWriter.Write(Utility.ReadEmbeddedResource("Org.Reddragonit.EmbeddedWebServer.resources.jquery.mobile.min.js"));
                        con.SendResponse();
                    }
                    else if (con.URL.AbsolutePath == "/json.js")
                    {
                        con.ResponseStatus = HttpStatusCodes.OK;
                        con.ResponseHeaders.ContentType = "text/javascript";
                        con.ResponseWriter.Write(Utility.ReadEmbeddedResource("Org.Reddragonit.EmbeddedWebServer.resources.json2.min.js"));
                        con.SendResponse();
                    }
                    else
                    {
                        DateTime start = DateTime.Now;
                        bool Processed = false;
                        if (_sites.Count > 1)
                        {
                            foreach (Site s in _sites)
                            {
                                if ((s.ServerName != null) && (s.ServerName == con.URL.Host))
                                {
                                    ProcessRequest(con, s, start);
                                    Processed = true;
                                    break;
                                }
                            }
                            if (!Processed)
                            {
                                foreach (Site s in _sites)
                                {
                                    if (s.Aliases != null)
                                    {
                                        foreach (string str in s.Aliases)
                                        {
                                            if (str == con.URL.Host)
                                            {
                                                ProcessRequest(con, s, start);
                                                Processed = true;
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                            if (!Processed)
                            {
                                foreach (Site s in _sites)
                                {
                                    foreach (sIPPortPair ipp in s.ListenOn)
                                    {
                                        if ((ipp.Address != IPAddress.Any) && (con.LocalEndPoint == new IPEndPoint(ipp.Address, ipp.Port)))
                                        {
                                            ProcessRequest(con, s, start);
                                            Processed = true;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        if (!Processed)
                            ProcessRequest(con, _defaultSite, start);
                    }
                }
            }
        }

        private void ProcessRequest(HttpConnection con, Site useSite, DateTime start)
        {
            Logger.LogMessage(DiagnosticsLevels.DEBUG,"Total time to find site: " + DateTime.Now.Subtract(start).TotalMilliseconds.ToString() + "ms");
            if ((!useSite.AllowGET && con.Method.ToUpper() == "GET") ||
                (!useSite.AllowPOST && con.Method.ToUpper() == "POST"))
            {
                con.ResponseStatus = HttpStatusCodes.Method_Not_Allowed;
                con.SendResponse();
            }
            else
            {
                start = DateTime.Now;
                if (con.URL.AbsolutePath == "/")
                    con.UseDefaultPath(useSite);
                Site.SetCurrentSite(useSite);
                try
                {
                    useSite.ProcessRequest(con);
                }
                catch (Exception e) {
                    con.ResponseStatus = HttpStatusCodes.Internal_Server_Error;
                    con.ResponseWriter.WriteLine(e.Message);
                    con.SendResponse();
                }
                Logger.LogMessage(DiagnosticsLevels.DEBUG, "Total time to process request to URL " + con.URL.AbsolutePath + " = " + DateTime.Now.Subtract(start).TotalMilliseconds.ToString() + "ms");
            }
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
                    _listener.EndAcceptTcpClient(null);
                }
                catch (Exception e)
                {
                    Logger.LogError(e);
                }
                _lastConnectionRefresh = DateTime.Now;
                _lastConnectionRequest = DateTime.Now;
                _listener.BeginAcceptTcpClient(new AsyncCallback(RecieveClient), null);
            }
        }
    }
}
