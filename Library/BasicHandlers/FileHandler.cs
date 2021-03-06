﻿using System;
using System.Collections.Generic;
using System.Text;
using Org.Reddragonit.EmbeddedWebServer.Interfaces;
using Org.Reddragonit.EmbeddedWebServer.Components;
using System.IO;
using Org.Reddragonit.EmbeddedWebServer.Components.Message;

namespace Org.Reddragonit.EmbeddedWebServer.BasicHandlers
{
    /*
     * This is just a generic file handler for a web site.  It will searve up pages contained 
     * within the given site path for the site, if it is specified.  It translates the extension of 
     * the file into the content type using a utility function.
     */
    public class FileHandler : IRequestHandler
    {
        private string TranslateURLPath(string url)
        {
            return url.Replace('/', Path.DirectorySeparatorChar);
        }

        #region IRequestHandler Members

        bool IRequestHandler.IsReusable
        {
            get { return true; }
        }

        bool IRequestHandler.CanProcessRequest(HttpRequest request, Site site)
        {
            if (site.BaseSitePath != null)
                return new FileInfo(site.BaseSitePath + Path.DirectorySeparatorChar.ToString() + TranslateURLPath(request.URL.AbsolutePath)).Exists;
            return false;
        }

        void IRequestHandler.ProcessRequest(HttpRequest request, Site site)
        {
            FileInfo fi = new FileInfo(site.BaseSitePath + Path.DirectorySeparatorChar.ToString() + TranslateURLPath(request.URL.AbsolutePath));
            request.ResponseHeaders.ContentType = HttpUtility.GetContentTypeForExtension(fi.Extension);
            BinaryReader br = new BinaryReader(new FileStream(fi.FullName,
                FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
            while (br.BaseStream.Position < br.BaseStream.Length)
            {
                byte[] buffer = br.ReadBytes(1024);
                request.ResponseWriter.BaseStream.Write(buffer, 0, buffer.Length);
            }
            br.Close();
        }

        void IRequestHandler.Init()
        {
        }

        void IRequestHandler.DeInit()
        {
        }

        bool IRequestHandler.RequiresSessionForRequest(HttpRequest request, Site site)
        {
            return false;
        }

        #endregion
    }
}
