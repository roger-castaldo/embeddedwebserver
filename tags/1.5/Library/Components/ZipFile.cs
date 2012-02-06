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

        private enum FileTypes { 
            File='0',
            Directory='5'
        }

        private struct sHeader
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
            public int gid
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

            private string LongToString(long val)
            {
                return AddChars(Convert.ToString(val, 8), 11, '0', true);
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

        private MemoryStream _ms;
        private BinaryWriter _bw;

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
        {
            _name = name;
            _ms = new MemoryStream();
            _bw = new BinaryWriter(new GZipStream(_ms, CompressionMode.Compress,true));
        }

        public Stream ToStream()
        {
            _bw.Flush();
            _bw.Close();
            _ms.Position = 0;
            return _ms;
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
    }
}
