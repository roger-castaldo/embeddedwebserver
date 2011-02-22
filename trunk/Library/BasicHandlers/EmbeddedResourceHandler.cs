using System;
using System.Collections.Generic;
using System.Text;
using Org.Reddragonit.EmbeddedWebServer.Interfaces;
using Org.Reddragonit.EmbeddedWebServer.Components;

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
                    break;
            }
        }

        void IRequestHandler.Init()
        {
        }

        #endregion
    }
}
