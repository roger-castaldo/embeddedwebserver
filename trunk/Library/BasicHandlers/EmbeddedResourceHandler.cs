using System;
using System.Collections.Generic;
using System.Text;
using Org.Reddragonit.EmbeddedWebServer.Interfaces;
using Org.Reddragonit.EmbeddedWebServer.Components;
using Org.Reddragonit.EmbeddedWebServer.Minifiers;
using System.IO;
using System.Threading;
using Org.Reddragonit.EmbeddedWebServer.Attributes;

namespace Org.Reddragonit.EmbeddedWebServer.BasicHandlers
{
    /*
     * This handler is implemented in order to respond to requests using virtual files
     * that are specified by the site in question, and are embedded resources contained
     * within a dll.
     */
    public class EmbeddedResourceHandler : IRequestHandler,IBackgroundOperationContainer
    {
        //the amount of minutes to cache any compressed items before removing them for efficiency
        private const int CACHE_EXPIRY_MINUTES = 60;

        //houses compressed css and js code from embedded resources
        private Dictionary<string, CachedItemContainer> _compressedCache;
        //a lock object used to control access to the compressed files cache
        private object _lock;

        public EmbeddedResourceHandler()
        {
            _lock = new object();
            _compressedCache = new Dictionary<string, CachedItemContainer>();
        }

        /*
         * A background thread operation designed to run through every instance 
         * of this type of handler and clean up the cached compressed information
         * by checking for expiry times.  This will run every minute.
         */
        [BackgroundOperationCall(-1, -1, -1, -1, BackgroundOperationDaysOfWeek.All)]
        public static void CleanupCache()
        {
            List<Site> sites = ServerControl.Sites;
            foreach (Site s in sites)
            {
                foreach (IRequestHandler handler in s.Handlers)
                {
                    if (handler is EmbeddedResourceHandler)
                    {
                        EmbeddedResourceHandler hand = (EmbeddedResourceHandler)handler;
                        Monitor.Enter(hand._lock);
                        if (hand._compressedCache != null)
                        {
                            string[] keys = new string[hand._compressedCache.Keys.Count];
                            hand._compressedCache.Keys.CopyTo(keys, 0);
                            foreach (string str in keys)
                            {
                                if (DateTime.Now.Subtract(hand._compressedCache[str].LastAccess).TotalMinutes > CACHE_EXPIRY_MINUTES)
                                    hand._compressedCache.Remove(str);
                            }
                        }
                        Monitor.Exit(hand._lock);
                        break;
                    }
                }
            }
        }

        #region IRequestHandler Members

        //this handler is reusable as nothing is done abnormally
        bool IRequestHandler.IsReusable
        {
            get { return true; }
        }

        //Checks through the list of embedded files from the site definition to 
        //see if any of the files available match the requested url
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

        /*
         * This function processes a given request for an embedded file.
         * It finds the file from the list, sets the response type appropriately.
         * If its uncompresssed css or js, checks the cache, if not compresses it,
         * and caches it.  It then writes the given file out to the response stream.
         */
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
                    bool loadCss = true;
                    Monitor.Enter(_lock);
                    if (_compressedCache.ContainsKey(file.Value.DLLPath))
                    {
                        loadCss = false;
                        conn.ResponseHeaders.ContentType = "text/css";
                        conn.ResponseWriter.Write(_compressedCache[file.Value.DLLPath].Value);
                    }
                    Monitor.Exit(_lock);
                    if (loadCss)
                    {
                        string css = Utility.ReadEmbeddedResource(file.Value.DLLPath);
                        if (css == null)
                            conn.ResponseStatus = HttpStatusCodes.Not_Found;
                        else
                        {
                            conn.ResponseHeaders.ContentType = "text/css";
                            css = CSSMinifier.Minify(css);
                            Monitor.Enter(_lock);
                            if (!_compressedCache.ContainsKey(file.Value.DLLPath))
                                _compressedCache.Add(file.Value.DLLPath, new CachedItemContainer(css));
                            Monitor.Exit(_lock);
                            conn.ResponseWriter.Write(css);
                        }
                    }
                    break;
                case EmbeddedFileTypes.Javascript:
                    bool loadJS = true;
                    Monitor.Enter(_lock);
                    if (_compressedCache.ContainsKey(file.Value.DLLPath))
                    {
                        loadJS = false;
                        conn.ResponseHeaders.ContentType = "text/javascript";
                        conn.ResponseWriter.Write(_compressedCache[file.Value.DLLPath].Value);
                    }
                    Monitor.Exit(_lock);
                    if (loadJS)
                    {
                        string js = Utility.ReadEmbeddedResource(file.Value.DLLPath);
                        if (js == null)
                            conn.ResponseStatus = HttpStatusCodes.Not_Found;
                        else
                        {
                            conn.ResponseHeaders.ContentType = "text/javascript";
                            js = JSMinifier.Minify(js);
                            Monitor.Enter(_lock);
                            if (!_compressedCache.ContainsKey(file.Value.DLLPath))
                                _compressedCache.Add(file.Value.DLLPath, new CachedItemContainer(js));
                            Monitor.Exit(_lock);
                            conn.ResponseWriter.Write(js);
                        }
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

        //nothing to do here
        void IRequestHandler.Init()
        {
        }

        //nothing to do here
        void IRequestHandler.DeInit()
        {
        }

        //always returns false as sessions are never necessary
        bool IRequestHandler.RequiresSessionForRequest(HttpConnection conn, Site site)
        {
            return false;
        }

        #endregion
    }
}
