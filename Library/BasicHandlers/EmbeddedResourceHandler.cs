using System;
using System.Collections.Generic;
using System.Text;
using Org.Reddragonit.EmbeddedWebServer.Interfaces;
using Org.Reddragonit.EmbeddedWebServer.Components;
using Org.Reddragonit.EmbeddedWebServer.Minifiers;
using System.IO;

namespace Org.Reddragonit.EmbeddedWebServer.BasicHandlers
{
    public class EmbeddedResourceHandler : IRequestHandler
    {
        #region IRequestHandler Members

        bool IRequestHandler.IsReusable
        {
            get { return true; }
        }

        bool IRequestHandler.CanProcessRequest(HttpConnection conn, Site site)
        {
            if (site.EmbeddedFiles != null)
            {
                foreach (sEmbeddedFile file in site.EmbeddedFiles)
                {
                    if (file.URL == conn.URL.AbsolutePath)
                        return true;
                }
            }
            return false;
        }

        void IRequestHandler.ProcessRequest(HttpConnection conn,Site site)
        {
            sEmbeddedFile? file = null;
            foreach (sEmbeddedFile ef in site.EmbeddedFiles)
            {
                if (ef.URL == conn.URL.AbsolutePath)
                {
                    file = ef;
                    break;
                }
            }
            switch (file.Value.FileType)
            {
                case EmbeddedFileTypes.Compressed_Css:
                    string comCss = Utility.ReadEmbeddedResource(file.Value.DLLPath);
                    if (comCss == null)
                        conn.ResponseStatus = HttpStatusCodes.Not_Found;
                    else
                    {
                        conn.ResponseHeaders.ContentType = "text/css";
                        conn.ResponseWriter.Write(comCss);
                    }
                    break;
                case EmbeddedFileTypes.Compressed_Javascript:
                    string comJs = Utility.ReadEmbeddedResource(file.Value.DLLPath);
                    if (comJs == null)
                        conn.ResponseStatus = HttpStatusCodes.Not_Found;
                    else
                    {
                        conn.ResponseHeaders.ContentType = "text/javascript";
                        conn.ResponseWriter.Write(comJs);
                    }
                    break;
                case EmbeddedFileTypes.Css:
                    string css = Utility.ReadEmbeddedResource(file.Value.DLLPath);
                    if (css == null)
                        conn.ResponseStatus = HttpStatusCodes.Not_Found;
                    else
                    {
                        conn.ResponseHeaders.ContentType = "text/css";
                        conn.ResponseWriter.Write(CSSMinifier.Minify(css));
                    }
                    break;
                case EmbeddedFileTypes.Javascript:
                    string js = Utility.ReadEmbeddedResource(file.Value.DLLPath);
                    if (js == null)
                        conn.ResponseStatus = HttpStatusCodes.Not_Found;
                    else
                    {
                        conn.ResponseHeaders.ContentType = "text/javascript";
                        conn.ResponseWriter.Write(JSMinifier.Minify(js));
                    }
                    break;
                case EmbeddedFileTypes.Image:
                    Stream str = Utility.LocateEmbededResource(file.Value.DLLPath);
                    if (str == null)
                        conn.ResponseStatus = HttpStatusCodes.Not_Found;
                    else
                    {
                        conn.ResponseHeaders.ContentType = "image/"+file.Value.ImageType.Value.ToString();
                        conn.UseResponseStream(str);
                    }
                    break;
                case EmbeddedFileTypes.Text:
                    string text = Utility.ReadEmbeddedResource(file.Value.DLLPath);
                    if (text == null)
                        conn.ResponseStatus = HttpStatusCodes.Not_Found;
                    else
                        conn.ResponseWriter.Write(text);
                    break;
            }
        }

        void IRequestHandler.Init()
        {
        }

        #endregion
    }
}
