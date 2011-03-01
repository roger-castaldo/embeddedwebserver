using System;
using System.Collections.Generic;
using System.Text;
using Org.Reddragonit.EmbeddedWebServer.Interfaces;
using System.Net;
using System.Net.Sockets;
using Org.Reddragonit.EmbeddedWebServer.Sessions;

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
        public void AttachSite(Site site)
        {
            if ((site.IPToListenTo == IPAddress.Any)||((IP!=IPAddress.Any)&&(site.IPToListenTo!=_ip)))
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
        public PortListener(Site site)
        {
            _sites = new List<Site>();
            _sites.Add(site);
            _port = site.Port;
            _ip = site.IPToListenTo;
        }

        //starts the listener by starting each site,
        //then binding the tcplistener to wait for incoming connections
        public void Start()
        {
            foreach (Site site in _sites)
                site.Start();
            _listener = new TcpListener(_ip, _port);
            _listener.Start();
            _listener.BeginAcceptTcpClient(new AsyncCallback(RecieveClient), null);
        }

        //stops the tcplistener from accepting connections and stops all sites contained within
        public void Stop()
        {
            try
            {
                _listener.EndAcceptTcpClient(null);
            }
            catch (Exception e) { }
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
            try
            {
                clnt = _listener.EndAcceptTcpClient(res);
            }
            catch (Exception e) {
                clnt = null;
            }
            try
            {
                _listener.BeginAcceptTcpClient(new AsyncCallback(RecieveClient), null);
            }
            catch (Exception e) { }
            if (clnt != null)
            {
                HttpConnection con = new HttpConnection(clnt);
                HttpConnection.SetCurrentConnection(con);
                if (con.URL.AbsolutePath == "/jquery.js")
                {
                    con.ResponseStatus = HttpStatusCodes.OK;
                    con.ResponseHeaders.ContentType = "text/javascript";
                    con.ResponseWriter.Write(Utility.ReadEmbeddedResource("Org.Reddragonit.EmbeddedWebServer.resources.jquery.min.js"));
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
                                if ((s.IPToListenTo != IPAddress.Any) && (con.LocalEndPoint == new IPEndPoint(s.IPToListenTo, s.Port)))
                                {
                                    ProcessRequest(con, s, start);
                                    Processed = true;
                                    break;
                                }
                            }
                        }
                    }
                    if (!Processed)
                        ProcessRequest(con, _defaultSite, start);
                }
            }
        }

        private void ProcessRequest(HttpConnection con, Site useSite, DateTime start)
        {
            System.Diagnostics.Debug.WriteLine("Total time to find site: " + DateTime.Now.Subtract(start).TotalMilliseconds.ToString() + "ms");
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
                useSite.ProcessRequest(con);
                System.Diagnostics.Debug.WriteLine("Total time to process request to URL "+con.URL.AbsolutePath+" = " + DateTime.Now.Subtract(start).TotalMilliseconds.ToString() + "ms");
            }
        }
    }
}
