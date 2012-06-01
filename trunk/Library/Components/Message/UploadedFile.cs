using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace Org.Reddragonit.EmbeddedWebServer.Components.Message
{
    /*
     * This class houses an uploaded file, specifying the parameter name of the file,
     * the file name and the content type.  All obtained from the file upload information
     * in the http stream.  It also contains a MemoryStream of the actual uploaded data.
     */
    public class UploadedFile
    {
        private string _parameterName;
        public string ParameterName
        {
            get { return _parameterName; }
        }

        private string _fileName;
        public string FileName
        {
            get { return _fileName; }
        }

        private string _contentType;
        public string ContentType
        {
            get { return _contentType; }
        }

        private Stream _stream;
        public Stream Stream
        {
            get { return _stream; }
        }

        public UploadedFile(string parName, string fileName, string contentType, MemoryStream stream)
        {
            _parameterName = parName;
            _fileName = fileName;
            _contentType = contentType;
            _stream = stream;
        }
    }
}
