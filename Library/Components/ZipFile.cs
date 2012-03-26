using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.IO.Compression;
using Org.Reddragonit.EmbeddedWebServer.Interfaces;

namespace Org.Reddragonit.EmbeddedWebServer.Components
{
    public class ZipFile
    {
        internal static readonly DateTime TheEpoch = new DateTime(1970, 1, 1, 0, 0, 0);

        public struct sZippedFile
        {
            private sHeader _header;
            private byte[] _data;
            public byte[] Data
            {
                get { return _data; }
            }

            public string Name
            {
                get { return _header.Name; }
            }

            public int Mode
            {
                get { return _header.Mode; }
            }

            public int UID
            {
                get { return _header.UID; }
            }

            public int GID
            {
                get { return _header.GID; }
            }

            public long Size
            {
                get { return _header.Size; }
            }

            public DateTime ModifyTime
            {
                get { return _header.ModifyTime; }
            }

            public short Version
            {
                get { return _header.Version; }
            }

            public string UName
            {
                get { return _header.UName; }
            }

            public string GName
            {
                get { return _header.GName; }
            }

            internal sZippedFile(sHeader header, byte[] data)
            {
                _header = header;
                _data = data;
            }
        }

        public struct sZippedFolder{
            private string _name;
            public string Name
            {
                get { return _name; }
            }

            private List<sZippedFolder> _folders;
            public List<sZippedFolder> Folders
            {
                get { return _folders; }
            }

            private List<sZippedFile> _files;
            public List<sZippedFile> Files
            {
                get { return _files; }
            }

            internal void AddFile(sHeader header, byte[] data)
            {
                string prefix = header.Prefix;
                if (prefix != null)
                {

                }
                _files.Add(new sZippedFile(header, data));
            }

            internal sZippedFolder(string name)
            {
                _name = name;
                _folders = new List<sZippedFolder>();
                _files = new List<sZippedFile>();
            }

            internal void AddFolder(string name,string prefix)
            {
                if ((prefix == _name)||(prefix==""))
                    _folders.Add(new sZippedFolder(name));
                else
                {
                    string[] split = prefix.Split('/');
                    prefix = prefix.Substring(prefix.IndexOf(split[0]));
                    prefix = prefix.TrimStart('/');
                    bool add = true;
                    for (int x = 0; x < _folders.Count; x++)
                    {
                        if (_folders[x].Name == split[0])
                        {
                            add = false;
                            _folders[x].AddFolder(name, prefix);
                            x = _folders.Count;
                        }
                    }
                    if (add)
                    {
                        sZippedFolder fold = new sZippedFolder(split[0]);
                        fold.AddFolder(name, prefix);
                        _folders.Add(fold);
                    }
                }
            }
        }

        internal enum FileTypes { 
            File='0',
            Directory='5'
        }

        internal struct sHeader
        {

            private string _name;
            public string Name
            {
                get { return _name; }
            }

            private int _mode;
            public int Mode
            {
                get { return _mode; }
            }

            private int _uid;
            public int UID
            {
                get { return _uid; }
            }

            private int _gid;
            public int GID
            {
                get { return _gid; }
            }

            private long _size;
            public long Size
            {
                get { return _size; }
            }

            private DateTime _modifyTime;
            public DateTime ModifyTime
            {
                get { return _modifyTime; }
            }

            private FileTypes _type;
            public FileTypes Type
            {
                get { return _type; }
            }

            private string _linkName;

            private short _version;
            public short Version
            {
                get { return _version; }
            }

            private string _uName;
            public string UName
            {
                get { return _uName; }
            }

            private string _gName;
            public string GName
            {
                get { return _gName; }
            }

            private string _prefix;
            public string Prefix
            {
                get { return _prefix; }
            }

            public sHeader(string fileName, byte[] data)
                : this(DateTime.Now, fileName, "", '/')
            {
                _size = data.Length;
                _type = FileTypes.File;
            }

            public sHeader(DirectoryInfo dir,string basePath):this(dir.LastWriteTime,dir.FullName,basePath,Path.DirectorySeparatorChar)
            {
                _size = 0;
                _type = FileTypes.Directory;
            }

            public sHeader(IDirectoryFolder folder,string basePath)
                : this(folder.CreateDate, Utility.TraceFullDirectoryPath(folder), basePath, '/')
            {
                _size = 0;
                _type = FileTypes.Directory;
            }

            public sHeader(FileInfo fi, string basePath):this(fi.LastWriteTime,fi.FullName,basePath,Path.DirectorySeparatorChar)
            {
                _size = fi.Length;
                _type = FileTypes.File;
            }

            public sHeader(IDirectoryFile file,string basePath)
                : this(file.CreateDate, Utility.TraceFullFilePath(file), basePath, '/')
            {
                _size = file.Length;
                _type = FileTypes.File;
            }

            private sHeader(DateTime modifyTime, string name, string basePath, char dirChar)
            {
                _type = FileTypes.File;
                _size = 0;
                _mode = 511;
                _uid = 61;
                _gid = 61;
                _uName = "root";
                _gName = "root";
                _modifyTime = modifyTime;
                name = name.Replace(Path.DirectorySeparatorChar, dirChar);
                basePath = basePath.Replace(Path.DirectorySeparatorChar, dirChar);
                name = (name.EndsWith(dirChar.ToString()) ? name.Substring(0, name.Length - 1) : name);
                name = name.Substring(basePath.Length);
                _prefix = "";
                if (name.Contains(dirChar.ToString()))
                {
                    _prefix = name.Substring(0, name.LastIndexOf(dirChar));
                    name = name.Substring(name.LastIndexOf(dirChar) + 1);
                }
                _name = name;
                _linkName = "";
                _version = BitConverter.ToInt16(ASCIIEncoding.ASCII.GetBytes("  "), 0);
                if (_name.Length > 100)
                    throw new Exception("Unable to compress when file/directory name is longer than 100 characters.");
                if (_prefix.Length > 155)
                    throw new Exception("Unable to compress when the path for a file/directory is longer than 100 characters.");
            }

            internal sHeader(byte[] headerData)
            {
                _name = ASCIIEncoding.ASCII.GetString(headerData, 0, 100).TrimEnd('\0');
                _mode = 0;
                _uid = 0;
                _gid = 0;
                _size = 0;
                _modifyTime = TheEpoch;
                _type = (FileTypes)headerData[156];
                _linkName = ASCIIEncoding.ASCII.GetString(headerData, 157, 100).TrimEnd('\0');
                _version = BitConverter.ToInt16(headerData, 263);
                _uName = ASCIIEncoding.ASCII.GetString(headerData, 265, 32).TrimEnd('\0');
                _gName = ASCIIEncoding.ASCII.GetString(headerData, 297, 32).TrimEnd('\0');
                _prefix = ASCIIEncoding.ASCII.GetString(headerData, 345, 155).TrimEnd('\0');
                _mode = StringToInt(ASCIIEncoding.ASCII.GetString(headerData, 100, 8));
                _uid = StringToInt(ASCIIEncoding.ASCII.GetString(headerData, 108, 8));
                _gid = StringToInt(ASCIIEncoding.ASCII.GetString(headerData, 116, 8));
                _size = StringToLong(ASCIIEncoding.ASCII.GetString(headerData, 124, 12));
                _modifyTime = TheEpoch.AddSeconds(StringToLong(ASCIIEncoding.ASCII.GetString(headerData, 136, 12)));
            }

            public byte[] Bytes
            {
                get
                {
                    byte[] ret = new byte[512];
                    ASCIIEncoding.ASCII.GetBytes(_name.PadRight(100, '\0')).CopyTo(ret, 0);
                    ASCIIEncoding.ASCII.GetBytes(IntToString(_mode)).CopyTo(ret, 100);
                    ASCIIEncoding.ASCII.GetBytes(IntToString(_uid)).CopyTo(ret, 108);
                    ASCIIEncoding.ASCII.GetBytes(IntToString(_gid)).CopyTo(ret, 116);
                    ASCIIEncoding.ASCII.GetBytes(LongToString(_size)).CopyTo(ret, 124);
                    ASCIIEncoding.ASCII.GetBytes(LongToString((long)(_modifyTime.Subtract(TheEpoch).TotalSeconds))).CopyTo(ret, 136);
                    ASCIIEncoding.ASCII.GetBytes("        ").CopyTo(ret,148);
                    ret[156] = (byte)_type;
                    ASCIIEncoding.ASCII.GetBytes(_linkName.PadRight(100,'\0')).CopyTo(ret,157);
                    ASCIIEncoding.ASCII.GetBytes("ustar").CopyTo(ret,257);
                    BitConverter.GetBytes(_version).CopyTo(ret,263);
                    ASCIIEncoding.ASCII.GetBytes(_uName.PadRight(32,'\0')).CopyTo(ret,265);
                    ASCIIEncoding.ASCII.GetBytes(_gName.PadRight(32,'\0')).CopyTo(ret,297);
                    ASCIIEncoding.ASCII.GetBytes(IntToString(0)).CopyTo(ret,329);
                    ASCIIEncoding.ASCII.GetBytes(IntToString(0)).CopyTo(ret,337);
                    ASCIIEncoding.ASCII.GetBytes(_prefix.PadRight(155,'\0')).CopyTo(ret,345);
                    long headerCheckSum=0;
                    foreach (byte b in ret){
                        if ((b&0x80)==0x80)
                            headerCheckSum-=(long)(b^0x80);
                        else
                            headerCheckSum+=(long)b;
                    }
                    ASCIIEncoding.ASCII.GetBytes(AddChars(Convert.ToString(headerCheckSum,8),6,'0',true)).CopyTo(ret,148);
                    return ret;
                }
            }

            private string IntToString(int val)
            {
                return AddChars(Convert.ToString(val, 8), 7, '0', true);
            }

            private int StringToInt(string val)
            {
                return Convert.ToInt32(val.TrimStart('0'), 8);
            }

            private string LongToString(long val)
            {
                return AddChars(Convert.ToString(val, 8), 11, '0', true);
            }

            private long StringToLong(string val)
            {
                return Convert.ToInt64(val.TrimStart('0'), 11);
            }

            private string AddChars(string str, int num, char ch, bool isLeading)
            {
                int neededZeroes = num - str.Length;
                while (neededZeroes > 0)
                {
                    if (isLeading)
                        str = ch + str;
                    else
                        str = str + ch;
                    --neededZeroes;
                }
                return str;
            }
        }

        private const byte _FILE_ID_TAG = 48;
        private const byte _DIRECTORY_ID_TAG = 53;

        private Stream _strm;
        private BinaryWriter _bw;
        private BinaryReader _br;

        private sZippedFolder _base;
        public List<sZippedFolder> Folders
        {
            get { return _base.Folders; }
        }

        public List<sZippedFile> Files
        {
            get { return _base.Files; }
        }

        public string Extension
        {
            get { return "tgz"; }
        }

        public string ContentType
        {
            get { return HttpUtility.GetContentTypeForExtension("tgz"); }
        }

        private string _name;
        public string Name
        {
            get { return _name; }
        }

        public ZipFile(string name)
            : this(new MemoryStream(),false)
        {
            _name = name;
        }

        public ZipFile(Stream strm, bool read)
        {
            _strm = strm;
            if (read)
                _bw = new BinaryWriter(new GZipStream(_strm, CompressionMode.Compress, true));
            else
            {
                _br = new BinaryReader(new GZipStream(_strm, CompressionMode.Decompress, true));
                _base = new sZippedFolder("");
                while (_br.BaseStream.Position < _br.BaseStream.Length)
                {
                    sHeader head = new sHeader(_br.ReadBytes(512));
                    switch (head.Type)
                    {
                        case FileTypes.Directory:
                            _base.AddFolder(head.Name, head.Prefix);
                            break;
                        case FileTypes.File:
                            _base.AddFile(head, _br.ReadBytes((int)head.Size));
                            break;
                    }
                }
            }
        }

        public Stream ToStream()
        {
            _bw.Flush();
            _bw.Close();
            Stream bstream = ((GZipStream)_strm).BaseStream;
            ((MemoryStream)bstream).Position = 0;
            return bstream;
        }

        public void Close()
        {
            if (_bw != null)
                _bw.Close();
            else
                _br.Close();
        }

        public void AddDirectory(DirectoryInfo di,string basePath)
        {
            _bw.Write(new sHeader(di, basePath).Bytes);
            foreach (DirectoryInfo d in di.GetDirectories())
                AddDirectory(d, basePath);
            foreach (FileInfo fi in di.GetFiles())
                AddFile(fi,basePath);
        }

        public void AddDirectory(IDirectoryFolder folder,string basePath)
        {
            _bw.Write(new sHeader(folder,basePath).Bytes);
            foreach (IDirectoryFolder fold in folder.Folders)
                AddDirectory(fold,basePath);
            foreach (IDirectoryFile file in folder.Files)
                AddFile(file,basePath);
        }

        public void AddFile(IDirectoryFile file,string basePath)
        {
            _bw.Write(new sHeader(file,basePath).Bytes);
            BinaryReader br = new BinaryReader(file.ContentStream);
            while (br.BaseStream.Position < br.BaseStream.Length)
            {
                byte[] data = br.ReadBytes(512);
                if (data.Length < 512)
                {
                    _bw.Write(data);
                    for (int x = 0; x < 512 - data.Length; x++)
                        _bw.Write((byte)0);
                }
                else
                    _bw.Write(data);
            }
        }

        public void AddFile(FileInfo fi, string basePath)
        {
            _bw.Write(new sHeader(fi, basePath).Bytes);
            BinaryReader br = new BinaryReader(fi.Open(FileMode.Open, FileAccess.Read, FileShare.Read));
            while (br.BaseStream.Position < br.BaseStream.Length)
            {
                byte[] data = br.ReadBytes(512);
                if (data.Length < 512)
                {
                    _bw.Write(data);
                    for (int x = 0; x < 512 - data.Length; x++)
                        _bw.Write((byte)0);
                }
                else
                    _bw.Write(data);
            }
        }

        public void AddFile(string name, byte[] data)
        {
            _bw.Write(new sHeader(name, data).Bytes);
            _bw.Write(data);
        }
    }
}
