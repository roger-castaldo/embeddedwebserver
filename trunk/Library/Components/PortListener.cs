using System;
using System.Collections.Generic;
using System.Text;
using Org.Reddragonit.EmbeddedWebServer.Interfaces;
using System.Net;
using System.Net.Sockets;
using Org.Reddragonit.EmbeddedWebServer.Sessions;

namespace Org.Reddragonit.EmbeddedWebServer.Components
{
    internal class PortListener
    {
        private TcpListener _listener;
        private List<Site> _sites;
        public List<Site> Sites
        {
            get { return _sites; }
        }

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

        public void AttachSite(Site site)
        {
            if ((site.IPToListenTo == IPAddress.Any)||((IP!=IPAddress.Any)&&(site.IPToListenTo!=_ip)))
                _ip = IPAddress.Any;
            _sites.Add(site);
        }

        private int _port;
        public int Port
        {
            get { return _port; }
        }

        private IPAddress _ip;
        public IPAddress IP
        {
            get { return _ip; }
        }

        public PortListener(Site site)
        {
            _sites = new List<Site>();
            _sites.Add(site);
            _port = site.Port;
            _ip = site.IPToListenTo;
        }

        public void Start()
        {
            foreach (Site site in _sites)
                site.Start();
            _listener = new TcpListener(_ip, _port);
            _listener.Start();
            _listener.BeginAcceptTcpClient(new AsyncCallback(RecieveClient), null);
        }

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
                DateTime start = DateTime.Now;
                Site useSite = null;
                foreach (Site s in _sites)
                {
                    if ((s.ServerName != null) && (s.ServerName == con.URL.Host))
                    {
                        useSite = s;
                        break;
                    }
                }
                if (useSite == null)
                {
                    foreach (Site s in _sites)
                    {
                        if ((s.IPToListenTo != IPAddress.Any) && (con.LocalEndPoint == new IPEndPoint(s.IPToListenTo, s.Port)))
                        {
                            useSite = s;
                            break;
                        }
                    }
                }
                if (useSite == null)
                    useSite = _defaultSite;
                System.Diagnostics.Debug.WriteLine("Total time to find site: " + DateTime.Now.Subtract(start).TotalMilliseconds.ToString() + "ms");
                start = DateTime.Now;
                Site.SetCurrentSite(useSite);
                useSite.ProcessRequest(con);
                System.Diagnostics.Debug.WriteLine("Total time to process request: " + DateTime.Now.Subtract(start).TotalMilliseconds.ToString() + "ms");
            }
        }
    }
}
