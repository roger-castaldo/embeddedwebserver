using System;
using System.Collections.Generic;
using System.Text;

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
}
