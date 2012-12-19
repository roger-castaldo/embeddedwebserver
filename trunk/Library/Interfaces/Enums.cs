using System;
using System.Collections.Generic;
using System.Text;

namespace Org.Reddragonit.EmbeddedWebServer.Interfaces
{
    //the list of file types for embedded files
    public enum EmbeddedFileTypes
    {
        Compressed_Javascript,
        Javascript,
        Compressed_Css,
        Css,
        Image,
        Text
    }

    //the list of available file image types that can be embedded
    public enum ImageTypes
    {
        png,
        bmp,
        jpeg,
        gif
    }

    //the list of available http auth types
    public enum HttpAuthTypes
    {
        Basic,
        Digest,
        None
    }
}
