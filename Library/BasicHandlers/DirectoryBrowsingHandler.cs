using System;
using System.Collections.Generic;
using System.Text;
using Org.Reddragonit.EmbeddedWebServer.Interfaces;
using Org.Reddragonit.EmbeddedWebServer.Components;
using System.IO;
using System.IO.Compression;

namespace Org.Reddragonit.EmbeddedWebServer.BasicHandlers
{
    public class DirectoryBrowsingHandler : IRequestHandler
    {
        private Dictionary<string, IDirectoryFolder> _paths;

        internal void DeployPath(string url, IDirectoryFolder folder)
        {
            url = (url.StartsWith("/") ? url : "/" + url);
            url = (url.EndsWith("/") ? url.Substring(0, url.Length - 1) : url);
            lock (_paths)
            {
                if (_paths.ContainsKey(url))
                    _paths.Remove(url);
                _paths.Add(url, folder);
            }
        }

        internal void RemovePath(string url)
        {
            url = (url.StartsWith("/") ? url : "/" + url);
            lock (_paths)
            {
                _paths.Remove(url);
            }
        }

        private Dictionary<string, Stream> _browserImages;
        private string _fileIconPath;
        private string _folderIconPath;
        private string _downloadIconPath;

        public DirectoryBrowsingHandler()
        {
            _paths = new Dictionary<string, IDirectoryFolder>();
            _browserImages = new Dictionary<string, Stream>();
            Random rand = new Random();
            _fileIconPath = "/"+Math.Abs(rand.Next()).ToString()+".png";
            _browserImages.Add(_fileIconPath, Utility.LocateEmbededResource("Org.Reddragonit.EmbeddedWebServer.resources.icons.file.png"));
            _folderIconPath = "/"+Math.Abs(rand.Next()).ToString()+".png";
            _browserImages.Add(_folderIconPath, Utility.LocateEmbededResource("Org.Reddragonit.EmbeddedWebServer.resources.icons.folder.png"));
            _downloadIconPath = "/" + Math.Abs(rand.Next()).ToString() + ".png";
            _browserImages.Add(_downloadIconPath, Utility.LocateEmbededResource("Org.Reddragonit.EmbeddedWebServer.resources.icons.download.png"));
        }

        #region IRequestHandler Members

        public bool IsReusable
        {
            get { return true; }
        }

        public bool CanProcessRequest(HttpConnection conn, Site site)
        {
            if (conn.URL.AbsolutePath == _fileIconPath)
                return true;
            else if (conn.URL.AbsolutePath == _folderIconPath)
                return true;
            else if (conn.URL.AbsolutePath == _downloadIconPath)
                return true;
            bool ret = false;
            lock (_paths)
            {
                foreach (string str in _paths.Keys)
                {
                    if (conn.URL.AbsolutePath.StartsWith(str))
                    {
                        ret = true;
                        conn["IFolder"] = _paths[str];
                        conn["IFolderPath"] = str;
                        break;
                    }
                }
            }
            return ret;
        }

        public void ProcessRequest(HttpConnection conn, Site site)
        {
            if (conn.URL.AbsolutePath == _fileIconPath)
            {
                conn.UseResponseStream(_browserImages[_fileIconPath]);
                conn.ResponseHeaders.ContentType = HttpUtility.GetContentTypeForExtension("png");
                return;
            }
            else if (conn.URL.AbsolutePath == _folderIconPath)
            {
                conn.UseResponseStream(_browserImages[_folderIconPath]);
                conn.ResponseHeaders.ContentType = HttpUtility.GetContentTypeForExtension("png");
                return;
            }
            else if (conn.URL.AbsolutePath == _downloadIconPath)
            {
                conn.UseResponseStream(_browserImages[_downloadIconPath]);
                conn.ResponseHeaders.ContentLength = HttpUtility.GetContentTypeForExtension("png");
                return;
            }
            IDirectoryFolder idf = (IDirectoryFolder)conn["IFolder"];
            string path = conn.URL.AbsolutePath;
            path = path.Substring(((string)conn["IFolderPath"]).Length);
            if (path.StartsWith("/"))
                path = path.Substring(1);
            IDirectoryFile ifile = null;
            IDirectoryFolder ifold = null;
            if (path == "" || path == "/")
                ifold = idf;
            else
                LocateObjectForPath((path.EndsWith("/") ? path : path + "/"), idf, out ifold, out ifile);
            if (conn.RequestParameters["DownloadPath"] != null)
            {
                path = (path.EndsWith("/") ? path.Substring(0,path.Length-1) : path);
                if (ifile != null)
                {
                    conn.UseResponseStream(ifile.ContentStream);
                    conn.ResponseHeaders.ContentType = HttpUtility.GetContentTypeForExtension(path.Substring(path.LastIndexOf(".")));
                }
                else
                {
                    ZipFile zf = new ZipFile(path.Substring(path.LastIndexOf("/") + 1));
                    string basePath = Utility.TraceFullDirectoryPath(ifold);
                    zf.AddDirectory(ifold,basePath.Substring(0,basePath.Length-ifold.Name.Length));
                    conn.UseResponseStream(zf.ToStream());
                    conn.ResponseHeaders.ContentType = zf.ContentType;
                    conn.ResponseHeaders["Content-Disposition"] = "attachment; filename=" + zf.Name+"."+zf.Extension;
                }
            }
            else
            {
                if (ifile != null)
                {
                    conn.UseResponseStream(ifile.ContentStream);
                    conn.ResponseHeaders.ContentType = HttpUtility.GetContentTypeForExtension(path.Substring(path.LastIndexOf(".")));
                }
                else if (ifold != null)
                {
                    conn.ResponseHeaders.ContentType = "text/html";
                    conn.ResponseWriter.Write(RenderFolderBrowser(ifold,site,conn));
                }
                else
                {
                    conn.ResponseStatus = HttpStatusCodes.Not_Found;
                    conn.ResponseWriter.WriteLine("<h1>Unable to locate the folder at the path " + conn.URL.AbsolutePath + "</h1>");
                }
            }
        }

        private void AppendCompresssedFolder(string path,StreamWriter sw,int index,IDirectoryFolder ifold)
        {
            foreach (IDirectoryFile file in ifold.Files)
            {
                sw.Write(Encoding.Default.GetBytes(index.ToString() + "," + path.TrimStart('/') + "/" + file.Name + "," + file.CreateDate.ToUniversalTime().ToString() + "," + file.Length.ToString()+"\n"));
                BinaryReader br = new BinaryReader(file.ContentStream);
                while (br.BaseStream.Position < br.BaseStream.Length)
                    sw.Write(br.ReadBytes(1024));
                sw.Write((byte)10);
                index++;
            }
            foreach (IDirectoryFolder folder in ifold.Folders)
            {
                AppendCompresssedFolder(path + "/" + folder.Name, sw, index, folder);
            }
        }

        private void LocateObjectForPath(string path, IDirectoryFolder folder, out IDirectoryFolder resFold, out IDirectoryFile resFile)
        {
            string dirname = path.Substring(0, path.IndexOf("/"));
            path = path.Substring(dirname.Length + 1);
            IDirectoryFolder[] folders = folder.Folders;
            IDirectoryFile[] files = folder.Files;
            folder = null;
            foreach (IDirectoryFolder id in folders)
            {
                if (id.Name == dirname)
                {
                    folder = id;
                    break;
                }
            }
            resFile = null;
            resFold = null;
            bool isFile = false;
            if (folder == null)
            {
                foreach (IDirectoryFile fi in files)
                {
                    if (fi.Name == dirname)
                    {
                        isFile = true;
                        resFile = fi;
                        break;
                    }
                }
            }
            if ((path == "/"||path=="") && !isFile)
                resFold = folder;
            if ((path != "/") && (resFold == null) && (resFile==null) && (folder!=null))
                LocateObjectForPath(path, folder, out resFold, out resFile);
                
        }

        private string RenderFolderBrowser(IDirectoryFolder folder,Site site,HttpConnection conn){
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<html><head><title>"+folder.Name+"</title>");
            sb.AppendLine("<script src=\"/jquery.js\" type=\"text/javascript\" language=\"javascript\"></script>");
            if (folder.StyleSheets != null)
            {
                foreach (string str in folder.StyleSheets)
                    sb.AppendLine("<link type=\"text/css\" rel=\"Stylesheet\" href=\"" + str + "\" />");
            }
            else
            {
                sb.AppendLine("<style>");
                sb.AppendLine("body{margin:5px;background-color:#FFFFFF;color:#000000;}");
                sb.AppendLine("a.Folder, a.File{background-repeat:no-repeat;background-position:left center;display:block;padding:0;margin:0;padding-left:20px;height:25px;border-bottom:solid 1px black;text-decoration:none;}");
                sb.AppendLine("a.Folder{background-image:url(\"" + _folderIconPath + "\");}");
                sb.AppendLine("a.File{background-image:url(\"" + _fileIconPath + "\");}");
                sb.AppendLine("a.Alt{background-color:#CCCCCC;color:#333333;}");
                sb.AppendLine("a.Folder:hover,a.File:hover{background-color:#555555;color:#CCCCCC;}");
                sb.AppendLine("div.vmenu{border:1px solid #aaa;position:absolute;background:#fff;	display:none;font-size:0.75em;height:20px;background-position:center left;background-image:url('"+_downloadIconPath+"');background-repeat:no-repeat;width:75px;padding-left:20px;cursor:pointer;}");
                sb.AppendLine("</style>");
            }
            sb.AppendLine("<script type=\"text/javascript\">");
            sb.AppendLine("function GetMousePosition(event){");
            sb.AppendLine("if (event.clientX!=undefined){ return {left:event.clientX,top:event.clientY};}");
            sb.AppendLine("else if (event.layerX!=undefined){ return {left:event.layerX,top:event.layerY};}");
            sb.AppendLine("else if (event.offsetX!=undefined){return {left:event.offsetX,top:event.offsetY};}");
            sb.AppendLine("else if (event.pageX!=undefined){return {left:event.pageX,top:event.pageY};}");
            sb.AppendLine("else if (event.screenX!=undefined){return {left:event.screenX,top:event.screenY};}");
            sb.AppendLine("else if (event.x!=undefined){return {left:event.x,top:event.y};}");
            sb.AppendLine("return null;}");
            sb.AppendLine("</script>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("<a class=\"Folder\" href=\"" + conn.URL.AbsolutePath + "\">.</a>");
            bool alt=true;
            if (folder.Parent != null)
            {
                alt = false;
                sb.AppendLine("<a class=\"Folder\" href=\"" + conn.URL.AbsolutePath.Substring(0, conn.URL.AbsolutePath.LastIndexOf("/")) + "\">..</a>");
            }
            foreach (IDirectoryFolder fold in folder.Folders)
            {
                sb.AppendLine("<a oncontextmenu=\"$(this).next().css({ left: GetMousePosition(event).left, top: GetMousePosition(event).top, zIndex: '101' }).show();return false;\" class=\"Folder " + (alt ? "Alt" : "") + "\" href=\"" + conn.URL.AbsolutePath + "/" + fold.Name + "\">" + fold.Name + " [" + fold.CreateDate.ToString("ddd, MMM dd yyyy") + "]</a>");
                alt = !alt;
                sb.AppendLine("<div class=\"vmenu\" onmouseout=\"$(this).hide();\" onclick=\"location.href='"+conn.URL.AbsolutePath+"/"+fold.Name+"/?DownloadPath=true';\">Download</div>");
            }
            foreach (IDirectoryFile file in folder.Files)
            {
                sb.AppendLine("<a class=\"File " + (alt ? "Alt" : "") + "\" href=\"" + conn.URL.AbsolutePath + "/" + file.Name + "\">" + file.Name + " [" + file.CreateDate.ToString("ddd, MMM dd yyyy") + "]</a>");
                alt = !alt;
            }
            sb.AppendLine("</body></html>");
            return sb.ToString();
        }

        public void Init()
        {
        }

        public void DeInit()
        {
        }

        public bool RequiresSessionForRequest(HttpConnection conn, Site site)
        {
            return false;
        }

        #endregion
    }
}
