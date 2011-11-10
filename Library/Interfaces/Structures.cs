using System;
using System.Collections.Generic;
using System.Text;
using System.Net;

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

        public sIPPortPair(IPAddress address, int port,bool useSSL)
        {
            _address = address;
            _port = port;
            _useSSL = useSSL;
        }
    }
}
