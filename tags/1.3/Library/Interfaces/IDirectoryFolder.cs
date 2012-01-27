using System;
using System.Collections.Generic;
using System.Text;

namespace Org.Reddragonit.EmbeddedWebServer.Interfaces
{
    public interface IDirectoryFolder
    {
        string Name { get; }
        DateTime CreateDate { get; }
        IDirectoryFile[] Files { get; }
        IDirectoryFolder[] Folders { get; }
        IDirectoryFolder Parent{ get; }
        string[] StyleSheets{get;}
    }
}
