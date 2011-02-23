using System;
using System.Collections.Generic;
using System.Text;
using Org.Reddragonit.EmbeddedWebServer.Interfaces;
using Org.Reddragonit.EmbeddedWebServer.Components;
using Org.Reddragonit.EmbeddedWebServer.Minifiers;
using System.IO;
using Org.Reddragonit.EmbeddedWebServer.Cache;
using System.Threading;

namespace Org.Reddragonit.EmbeddedWebServer.BasicHandlers
{
    public class EmbeddedResourceHandler : IRequestHandler
    {
        private const int THREAD_SLEEP = 60000;
        private const int CACHE_EXPIRY_MINUTES = 60;

        private Dictionary<string, CachedItemContainer> _compressedCache;
        private object _lock;
        private bool _threadExit;
        private Thread _cleanupThread;

        public EmbeddedResourceHandler()
        {
            _lock = new object();
            _compressedCache = new Dictionary<string, CachedItemContainer>();
            _threadExit = false;
            _cleanupThread = new Thread(new ThreadStart(CleanupThreadStart));
            _cleanupThread.Start();
        }

        ~EmbeddedResourceHandler()
        {
            Monitor.Exit(_lock);
            _threadExit = true;
            Monitor.Exit(_lock);
            try
            {
                _cleanupThread.Join();
            }
            catch (Exception e) { }
            _compressedCache = null;
            GC.Collect();
        }

        private void CleanupThreadStart()
        {
            while (!_threadExit)
            {
                Monitor.Enter(_lock);
                if (_compressedCache != null)
                {
                    string[] keys = new string[_compressedCache.Keys.Count];
                    _compressedCache.Keys.CopyTo(keys, 0);
                    foreach (string str in keys)
                    {
                        if (DateTime.Now.Subtract(_compressedCache[str].LastAccess).TotalMinutes > CACHE_EXPIRY_MINUTES)
                            _compressedCache.Remove(str);
                    }
                }
                Monitor.Exit(_lock);
                Thread.Sleep(THREAD_SLEEP);
            }
        }

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

        void IRequestHandler.Init()
        {
        }

        void IRequestHandler.DeInit()
        {
        }

        #endregion
    }
}
