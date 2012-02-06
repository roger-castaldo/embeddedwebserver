using System;
using System.Collections.Generic;
using System.Text;
using Org.Reddragonit.EmbeddedWebServer.Interfaces;
using System.IO;

namespace Tester
{
    public class PhysicalDirectoryFile : IDirectoryFile
    {
        private FileInfo _file;
        private IDirectoryFolder _folder;

        public PhysicalDirectoryFile(FileInfo file,PhysicalDirectoryFolder folder)
        {
            _file = file;
            _folder = folder;
        }

        #region IDirectoryFile Members

        public string Name
        {
            get { return _file.Name; }
        }

        public DateTime CreateDate
        {
            get { return _file.CreationTime; }
        }

        public Stream ContentStream
        {
            get { return (Stream)_file.OpenRead(); }
        }

        public long Length
        {
            get { return _file.Length; }
        }

        public IDirectoryFolder Folder
        {
            get { return _folder; }
        }

        #endregion
    }
}
