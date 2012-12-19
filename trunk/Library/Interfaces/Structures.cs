using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Security.Cryptography;

namespace Org.Reddragonit.EmbeddedWebServer.Interfaces
{
    public struct sEmbeddedFile
    {
        private string _dllPath;
        public string DLLPath
        {
            get { return _dllPath; }
        }

        private string _url;
        public string URL
        {
            get { return _url; }
        }

        private EmbeddedFileTypes _fileType;
        public EmbeddedFileTypes FileType
        {
            get { return _fileType; }
        }

        private ImageTypes? _imageType;
        public ImageTypes? ImageType
        {
            get { return _imageType; }
        }

        public sEmbeddedFile(string dllpath, string url, EmbeddedFileTypes fileType, ImageTypes? imageType)
        {
            _dllPath = dllpath;
            _url = url;
            _fileType = fileType;
            _imageType = imageType;
        }
    }

    public struct sIPPortPair
    {
        private int _port;
        public int Port
        {
            get { return _port; }
        }

        private IPAddress _address;
        public IPAddress Address
        {
            get { return _address; }
        }

        private bool _useSSL;
        public bool UseSSL
        {
            get { return _useSSL; }
        }

        private long _idleSeonds;
        public long IdleSeconds
        {
            get { return _idleSeonds; }
        }
        
        private long _totalRunSeconds;
        public long TotalRunSeconds
        {
            get { return _totalRunSeconds; }
        }

        private int _backlog;
        public int BackLog
        {
            get { return _backlog; }
        }

        public sIPPortPair(IPAddress address, int port,bool useSSL)
            : this(address,port,useSSL,null,null,null)
        {
        }

        public sIPPortPair(IPAddress address, int port, bool useSSL, long? idleSeconds, long? totalRunSeconds,int? backLog)
        {
            _address = address;
            _port = port;
            _useSSL = useSSL;
            _idleSeonds = (idleSeconds.HasValue ? idleSeconds.Value : (long)(60 * 60));
            _totalRunSeconds = (totalRunSeconds.HasValue ? totalRunSeconds.Value : (long)(24 * 60 * 60));
            _backlog = (backLog.HasValue ? backLog.Value : 20);
        }
    }

    public struct sHttpAuthUsernamePassword
    {
        private string _username;
        public string UserName
        {
            get { return _username; }
        }

        private string _password;
        public string Password
        {
            get { return _password; }
        }

        public sHttpAuthUsernamePassword(string username, string password)
        {
            _username = username;
            _password = password;
            _basicAuthString = Convert.ToBase64String(ASCIIEncoding.ASCII.GetBytes(_username + ":" + _password));
        }

        private string _basicAuthString;
        internal string BasicAuthorizationString
        {
            get { return _basicAuthString; }
        }

        internal string GetDigestString(string realm, string method,string uri, string nonce)
        {
            MD5 m = MD5.Create();
            string ha1 = BitConverter.ToString(m.ComputeHash(ASCIIEncoding.ASCII.GetBytes(_username + ":" + realm + ":" + _password))).Replace("-", "").ToLower();
            string ha2 = BitConverter.ToString(m.ComputeHash(ASCIIEncoding.ASCII.GetBytes(method + ":" + uri))).Replace("-", "").ToLower();
            return BitConverter.ToString(m.ComputeHash(ASCIIEncoding.ASCII.GetBytes(ha1 + ":" + nonce + ":" + ha2))).Replace("-", "").ToLower();
        }
    }
}
