using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace Org.Reddragonit.EmbeddedWebServer.Interfaces
{
    public interface IDirectoryFile
    {
        string Name { get; }
        DateTime CreateDate { get; }
        Stream ContentStream { get; }
        long Length { get; }
        IDirectoryFolder Folder { get; }
    }
}
