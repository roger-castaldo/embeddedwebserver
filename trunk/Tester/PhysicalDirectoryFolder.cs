using System;
using System.Collections.Generic;
using System.Text;
using Org.Reddragonit.EmbeddedWebServer.Interfaces;
using System.IO;

namespace Tester
{
    public class PhysicalDirectoryFolder : IDirectoryFolder
    {
        private DirectoryInfo _dir;
        private IDirectoryFolder _parent;

        public PhysicalDirectoryFolder(DirectoryInfo dir,PhysicalDirectoryFolder parent)
        {
            _dir = dir;
            _parent = parent;
        }

        #region IDirectoryFolder Members

        public string Name
        {
            get { return _dir.Name; }
        }

        public DateTime CreateDate
        {
            get { return _dir.CreationTime; }
        }

        public IDirectoryFile[] Files
        {
            get {
                List<IDirectoryFile> ret = new List<IDirectoryFile>();
                foreach (FileInfo fi in _dir.GetFiles())
                    ret.Add(new PhysicalDirectoryFile(fi, this));
                return ret.ToArray();
            }
        }

        public IDirectoryFolder[] Folders
        {
            get {
                List<IDirectoryFolder> ret = new List<IDirectoryFolder>();
                foreach (DirectoryInfo di in _dir.GetDirectories())
                    ret.Add(new PhysicalDirectoryFolder(di,this));
                return ret.ToArray();
            }
        }

        public IDirectoryFolder Parent
        {
            get { return _parent; }
        }

        public string[] StyleSheets
        {
            get { return null; }
        }

        #endregion
    }
}
