using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.IO.Compression;
using Org.Reddragonit.EmbeddedWebServer.Interfaces;

namespace Org.Reddragonit.EmbeddedWebServer.Components
{
    internal class TarHeader
    {
        #region Constants
        public const int NAMELEN = 100;
        public const int MODELEN = 8;
        public const int UIDLEN = 8;
        public const int GIDLEN = 8;
        public const int CHKSUMLEN = 8;
        public const int CHKSUMOFS = 148;
        public const int SIZELEN = 12;
        public const int MAGICLEN = 6;
        public const int VERSIONLEN = 2;
        public const int MODTIMELEN = 12;
        public const int UNAMELEN = 32;
        public const int GNAMELEN = 32;
        public const int DEVLEN = 8;
        public const int PREFIXLEN = 155;
        private const char dirChar = '/';

        public enum EntryTypes : byte
        {
            NORMAL = (byte)'0',
            DIRECTORY = (byte)'5'
        }

        public const string TMAGIC = "ustar";
        public const string GNU_TMAGIC = "ustar";

        const long timeConversionFactor = 10000000L; // 1 tick == 100 nanoseconds
        readonly static DateTime dateTime1970 = new DateTime(1970, 1, 1, 0, 0, 0, 0);
        #endregion

        #region Constructors
        public TarHeader(string fileName, byte[] data)
            : this(DateTime.Now, fileName, "")
        {
            _size = data.Length;
            _typeFlag = (byte)EntryTypes.NORMAL;
        }

        public TarHeader(DirectoryInfo dir, string basePath)
            : this(dir.LastWriteTime, dir.FullName, basePath)
        {
            _size = 0;
            _typeFlag = (byte)EntryTypes.DIRECTORY;
            _name = (_name.EndsWith("/") ? _name : _name + "/");
        }

        public TarHeader(IDirectoryFolder dir,string basePath)
            : this(dir.CreateDate, Utility.TraceFullDirectoryPath(dir), basePath)
        {
            _size = 0;
            _typeFlag = (byte)EntryTypes.DIRECTORY;
            _name = (_name.EndsWith("/") ? _name : _name + "/");
        }

        public TarHeader(IDirectoryFile fi, string basePath)
            : this(fi.CreateDate,Utility.TraceFullFilePath(fi),basePath)
        {
            _size = fi.Length;
        }

        public TarHeader(FileInfo fi, string basePath)
            : this(fi.LastWriteTime, fi.FullName, basePath)
        {
            _size = fi.Length;
            _typeFlag = (byte)EntryTypes.NORMAL;
        }

        public TarHeader()
        {
            _magic = TMAGIC;
            _version = " ";

            _name = "";
            _linkName = "";

            _mode = 511;
            _userId = 0;
            _groupId = 0;
            _userName = "";
            _groupName = "";
            _size = 0;
            _linkName = "";
        }

        private TarHeader(DateTime modifyTime, string name, string basePath)
            : this()
        {
            _typeFlag = (byte)EntryTypes.NORMAL;
            _modTime = modifyTime;
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
            if (_name.Length > 100)
                throw new Exception("Unable to compress when file/directory name is longer than 100 characters.");
            if (_prefix.Length > 155)
                throw new Exception("Unable to compress when the path for a file/directory is longer than 100 characters.");
        }

        internal TarHeader(byte[] headerData)
        {
            ParseBuffer(headerData);
        }


        #endregion

        #region Properties
        private string _name;
        public string Name { get { return _name; } }

        private int _mode;
        public int Mode { get { return _mode; } }

        private int _userId;
        public int UserId { get { return _userId; } }

        private int _groupId;
        public int GroupId { get { return _groupId; } }

        private long _size;
        public long Size { get { return _size; } }

        private DateTime _modTime;
        public DateTime ModTime { get { return _modTime; } }

        private int _checksum;
        public int Checksum { get { return _checksum; } }

        private bool _isChecksumValid;
        public bool IsChecksumValid { get { return _isChecksumValid; } }

        private byte _typeFlag;
        public EntryTypes TypeFlag { get { return (EntryTypes)_typeFlag; } }

        private string _linkName;
        public string LinkName { get { return _linkName; } }

        private string _magic;
        public string Magic { get { return _magic; } }

        private string _version;
        public string Version { get { return _version; } }

        private string _userName;
        public string UserName { get { return _userName; } }

        private string _groupName;
        public string GroupName { get { return _groupName; } }

        private int _devMajor;
        public int DevMajor { get { return _devMajor; } }

        private int _devMinor;
        public int DevMinor { get { return _devMinor; } }

        private string _prefix;
        public string Prefix { get { return _prefix; } }

        #endregion

        public void ParseBuffer(byte[] header)
        {
            if (header == null)
                throw new ArgumentNullException("header");

            int offset = 0;

            _name = ParseName(header, offset, NAMELEN).ToString();
            offset += NAMELEN;

            _mode = (int)ParseOctal(header, offset, MODELEN);
            offset += MODELEN;

            _userId = (int)ParseOctal(header, offset, UIDLEN);
            offset += UIDLEN;

            _groupId = (int)ParseOctal(header, offset, GIDLEN);
            offset += GIDLEN;

            _size = ParseBinaryOrOctal(header, offset, SIZELEN);
            offset += SIZELEN;

            _modTime = GetDateTimeFromCTime(ParseOctal(header, offset, MODTIMELEN));
            offset += MODTIMELEN;

            _checksum = (int)ParseOctal(header, offset, CHKSUMLEN);
            offset += CHKSUMLEN;

            _typeFlag = header[offset++];

            _linkName = ParseName(header, offset, NAMELEN).ToString();
            offset += NAMELEN;

            _magic = ParseName(header, offset, MAGICLEN).ToString();
            offset += MAGICLEN;

            _version = ParseName(header, offset, VERSIONLEN).ToString();
            offset += VERSIONLEN;

            _userName = ParseName(header, offset, UNAMELEN).ToString();
            offset += UNAMELEN;

            _groupName = ParseName(header, offset, GNAMELEN).ToString();
            offset += GNAMELEN;

            _devMajor = (int)ParseOctal(header, offset, DEVLEN);
            offset += DEVLEN;

            _devMinor = (int)ParseOctal(header, offset, DEVLEN);
            offset += DEVLEN;

            _prefix = ParseName(header, offset, PREFIXLEN).ToString();

            _isChecksumValid = Checksum == TarHeader.MakeCheckSum(header);
        }

        public byte[] Bytes
        {
            get
            {
                byte[] outBuffer = new byte[512];
                int offset = 0;

                offset = GetNameBytes(Name, outBuffer, offset, NAMELEN);
                offset = GetOctalBytes(Mode, outBuffer, offset, MODELEN);
                offset = GetOctalBytes(UserId, outBuffer, offset, UIDLEN);
                offset = GetOctalBytes(GroupId, outBuffer, offset, GIDLEN);

                offset = GetBinaryOrOctalBytes(Size, outBuffer, offset, SIZELEN);
                offset = GetOctalBytes(GetCTime(ModTime), outBuffer, offset, MODTIMELEN);

                int csOffset = offset;
                for (int c = 0; c < CHKSUMLEN; ++c)
                {
                    outBuffer[offset++] = (byte)' ';
                }

                outBuffer[offset++] = _typeFlag;

                offset = GetNameBytes(LinkName, outBuffer, offset, NAMELEN);
                offset = GetAsciiBytes(Magic, 0, outBuffer, offset, MAGICLEN);
                offset = GetNameBytes(Version, outBuffer, offset, VERSIONLEN);
                offset = GetNameBytes(UserName, outBuffer, offset, UNAMELEN);
                offset = GetNameBytes(GroupName, outBuffer, offset, GNAMELEN);
                offset = GetOctalBytes(0, outBuffer, offset, DEVLEN);
                offset = GetOctalBytes(0, outBuffer, offset, DEVLEN);

                offset = GetNameBytes(Prefix, outBuffer, offset, PREFIXLEN);

                for (; offset < outBuffer.Length; )
                    outBuffer[offset++] = 0;

                _checksum = ComputeCheckSum(outBuffer);

                GetCheckSumOctalBytes(_checksum, outBuffer, csOffset, CHKSUMLEN);
                _isChecksumValid = true;
                return outBuffer;
            }
        }

        public override int GetHashCode()
        {
            return ((Prefix == null ? Path.DirectorySeparatorChar.ToString() : Prefix + Path.DirectorySeparatorChar.ToString()) + Name).GetHashCode();
        }

        public override bool Equals(object obj)
        {
            TarHeader localHeader = obj as TarHeader;

            bool result;
            if (localHeader != null)
            {
                result = (Name == localHeader.Name)
                        && (Mode == localHeader.Mode)
                        && (UserId == localHeader.UserId)
                        && (GroupId == localHeader.GroupId)
                        && (Size == localHeader.Size)
                        && (ModTime == localHeader.ModTime)
                        && (Checksum == localHeader.Checksum)
                        && (TypeFlag == localHeader.TypeFlag)
                        && (LinkName == localHeader.LinkName)
                        && (Magic == localHeader.Magic)
                        && (Version == localHeader.Version)
                        && (UserName == localHeader.UserName)
                        && (GroupName == localHeader.GroupName)
                        && (DevMajor == localHeader.DevMajor)
                        && (DevMinor == localHeader.DevMinor)
                        && (Prefix == localHeader.Prefix);
            }
            else
            {
                result = false;
            }
            return result;
        }

        static private long ParseBinaryOrOctal(byte[] header, int offset, int length)
        {
            if (header[offset] >= 0x80)
            {
                // File sizes over 8GB are stored in 8 right-justified bytes of binary indicated by setting the high-order bit of the leftmost byte of a numeric field.
                long result = 0;
                for (int pos = length - 8; pos < length; pos++)
                {
                    result = result << 8 | header[offset + pos];
                }
                return result;
            }
            return ParseOctal(header, offset, length);
        }

        static public long ParseOctal(byte[] header, int offset, int length)
        {
            if (header == null)
            {
                throw new ArgumentNullException("header");
            }

            long result = 0;
            bool stillPadding = true;

            int end = offset + length;
            for (int i = offset; i < end; ++i)
            {
                if (header[i] == 0)
                {
                    break;
                }

                if (header[i] == (byte)' ' || header[i] == '0')
                {
                    if (stillPadding)
                    {
                        continue;
                    }

                    if (header[i] == (byte)' ')
                    {
                        break;
                    }
                }

                stillPadding = false;

                result = (result << 3) + (header[i] - '0');
            }

            return result;
        }

        static public StringBuilder ParseName(byte[] header, int offset, int length)
        {
            if (header == null)
                throw new ArgumentNullException("header");

            if (offset < 0)
                throw new ArgumentOutOfRangeException("offset", "Cannot be less than zero");

            if (length < 0)
                throw new ArgumentOutOfRangeException("length", "Cannot be less than zero");

            if (offset + length > header.Length)
                throw new ArgumentException("Exceeds header size", "length");

            StringBuilder result = new StringBuilder(length);

            for (int i = offset; i < offset + length; ++i)
            {
                if (header[i] == 0)
                    break;
                result.Append((char)header[i]);
            }

            return result;
        }

        public static int GetNameBytes(StringBuilder name, int nameOffset, byte[] buffer, int bufferOffset, int length)
        {
            if (name == null)
                throw new ArgumentNullException("name");

            if (buffer == null)
                throw new ArgumentNullException("buffer");

            return GetNameBytes(name.ToString(), nameOffset, buffer, bufferOffset, length);
        }

        public static int GetNameBytes(string name, int nameOffset, byte[] buffer, int bufferOffset, int length)
        {
            if (name == null)
                throw new ArgumentNullException("name");

            if (buffer == null)
                throw new ArgumentNullException("buffer");

            int i;

            for (i = 0; i < length - 1 && nameOffset + i < name.Length; ++i)
            {
                buffer[bufferOffset + i] = (byte)name[nameOffset + i];
            }

            for (; i < length; ++i)
            {
                buffer[bufferOffset + i] = 0;
            }

            return bufferOffset + length;
        }

        public static int GetNameBytes(StringBuilder name, byte[] buffer, int offset, int length)
        {
            if (name == null)
                throw new ArgumentNullException("name");

            if (buffer == null)
                throw new ArgumentNullException("buffer");

            return GetNameBytes(name.ToString(), 0, buffer, offset, length);
        }

        public static int GetNameBytes(string name, byte[] buffer, int offset, int length)
        {
            if (name == null)
                throw new ArgumentNullException("name");

            if (buffer == null)
                throw new ArgumentNullException("buffer");

            return GetNameBytes(name, 0, buffer, offset, length);
        }

        public static int GetAsciiBytes(string toAdd, int nameOffset, byte[] buffer, int bufferOffset, int length)
        {
            if (toAdd == null)
                throw new ArgumentNullException("toAdd");

            if (buffer == null)
                throw new ArgumentNullException("buffer");

            for (int i = 0; i < length && nameOffset + i < toAdd.Length; ++i)
                buffer[bufferOffset + i] = (byte)toAdd[nameOffset + i];
            return bufferOffset + length;
        }

        public static int GetOctalBytes(long value, byte[] buffer, int offset, int length)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");

            int localIndex = length - 1;

            // Either a space or null is valid here. We use NULL as per GNUTar
            buffer[offset + localIndex] = 0;
            --localIndex;

            if (value > 0)
            {
                for (long v = value; (localIndex >= 0) && (v > 0); --localIndex)
                {
                    buffer[offset + localIndex] = (byte)((byte)'0' + (byte)(v & 7));
                    v >>= 3;
                }
            }

            for (; localIndex >= 0; --localIndex)
            {
                buffer[offset + localIndex] = (byte)'0';
            }

            return offset + length;
        }

        private static int GetBinaryOrOctalBytes(long value, byte[] buffer, int offset, int length)
        {
            if (value > 0x1FFFFFFFF)
            {        // Octal 77777777777 (11 digits)
                // Put value as binary, right-justified into the buffer. Set high order bit of left-most byte.
                for (int pos = length - 1; pos > 0; pos--)
                {
                    buffer[offset + pos] = (byte)value;
                    value = value >> 8;
                }
                buffer[offset] = 0x80;
                return offset + length;
            }
            return GetOctalBytes(value, buffer, offset, length);
        }

        static void GetCheckSumOctalBytes(long value, byte[] buffer, int offset, int length)
        {
            GetOctalBytes(value, buffer, offset, length - 1);
        }

        static int ComputeCheckSum(byte[] buffer)
        {
            int sum = 0;
            for (int i = 0; i < buffer.Length; ++i)
            {
                sum += buffer[i];
            }
            return sum;
        }

        static int MakeCheckSum(byte[] buffer)
        {
            int sum = 0;
            for (int i = 0; i < CHKSUMOFS; ++i)
                sum += buffer[i];

            for (int i = 0; i < CHKSUMLEN; ++i)
                sum += (byte)' ';

            for (int i = CHKSUMOFS + CHKSUMLEN; i < buffer.Length; ++i)
                sum += buffer[i];
            return sum;
        }

        static int GetCTime(DateTime dateTime)
        {
            return unchecked((int)((dateTime.Ticks - dateTime1970.Ticks) / timeConversionFactor));
        }

        static DateTime GetDateTimeFromCTime(long ticks)
        {
            DateTime result;

            try
            {
                result = new DateTime(dateTime1970.Ticks + ticks * timeConversionFactor);
            }
            catch (ArgumentOutOfRangeException)
            {
                result = dateTime1970;
            }
            return result;
        }
    }

    public class sZippedFolder
    {
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

        private List<ZipFile.sZippedFile> _files;
        public List<ZipFile.sZippedFile> Files
        {
            get { return _files; }
        }

        internal void AddFile(TarHeader header, byte[] data)
        {
            string prefix = header.Prefix;
            if (prefix != null)
            {
                if ((prefix == _name) || (prefix == ""))
                    _files.Add(new ZipFile.sZippedFile(header, data));
                else
                {
                    AddFolder(prefix.Substring(prefix.LastIndexOf('/')), prefix.Substring(0, prefix.LastIndexOf('/')));
                    string[] split = prefix.Trim('/').Split('/');
                    sZippedFolder fold = this;
                    int x = 0;
                    if (split[x] == _name)
                        x++;
                    while (x < split.Length)
                    {
                        List<sZippedFolder> folds = fold.Folders;
                        foreach (sZippedFolder szf in folds)
                        {
                            if (szf.Name == split[x])
                            {
                                fold = szf;
                                break;
                            }
                        }
                        x++;
                    }
                    fold._files.Add(new ZipFile.sZippedFile(header, data));
                }
            }
            else
                _files.Add(new ZipFile.sZippedFile(header, data));
        }

        internal sZippedFolder(string name)
        {
            _name = name;
            _folders = new List<sZippedFolder>();
            _files = new List<ZipFile.sZippedFile>();
        }

        internal void AddFolder(string name, string prefix)
        {
            if ((prefix == _name) || (prefix == ""))
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

    public class ZipFile
    {
        internal static readonly DateTime TheEpoch = new DateTime(1970, 1, 1, 0, 0, 0);

        public struct sZippedFile
        {
            private TarHeader _header;
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
                get { return _header.UserId; }
            }

            public int GID
            {
                get { return _header.GroupId; }
            }

            public long Size
            {
                get { return _header.Size; }
            }

            public DateTime ModifyTime
            {
                get { return _header.ModTime; }
            }

            public string Version
            {
                get { return _header.Version; }
            }

            public string UName
            {
                get { return _header.UserName; }
            }

            public string GName
            {
                get { return _header.GroupName; }
            }

            internal sZippedFile(TarHeader header, byte[] data)
            {
                _header = header;
                _data = data;
            }
        }

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
            get { return HttpUtility.GetContentTypeForExtension(Extension); }
        }

        private string _name;
        public string Name
        {
            get { return _name; }
        }

        public ZipFile(string name)
            : this(new MemoryStream(), false)
        {
            _name = name;
        }

        public ZipFile(Stream strm, bool read)
        {
            _strm = strm;
            if (!read)
                _bw = new BinaryWriter(new GZipStream(_strm, CompressionMode.Compress, true));
            else
            {
                _br = new BinaryReader(new GZipStream(_strm, CompressionMode.Decompress, true));
                _base = new sZippedFolder("");
                while (true)
                {
                    byte[] bhead = new byte[512];
                    int bread = _br.Read(bhead, 0, 512);
                    if (bread == 0)
                        break;
                    TarHeader head = new TarHeader(bhead);
                    switch (head.TypeFlag)
                    {
                        case TarHeader.EntryTypes.DIRECTORY:
                            _base.AddFolder(head.Name, head.Prefix);
                            break;
                        case TarHeader.EntryTypes.NORMAL:
                            byte[] data = _br.ReadBytes((int)head.Size);
                            int size = 0;
                            while ((data.Length + size) % 512 != 0)
                            {
                                _br.ReadByte();
                                size++;
                            }
                            _base.AddFile(head, data);
                            break;

                    }
                }
            }
        }

        public Stream ToStream()
        {
            Flush();
            Close();
            Stream bstream = ((GZipStream)_strm).BaseStream;
            ((MemoryStream)bstream).Position = 0;
            return bstream;
        }

        internal void AlignTo512(long size, bool acceptZero)
        {
            size = size % 512;
            if (size == 0 && !acceptZero) return;
            while (size < 512)
            {
                _bw.Write((byte)0);
                size++;
            }
        }

        private bool _isClosed = false;
        public void Flush()
        {
            if (!_isClosed)
            {
                AlignTo512(0, true);
                AlignTo512(0, true);
                _bw.Flush();
                _isClosed = true;
            }
        }

        public void Close()
        {
            if (_bw != null)
                _bw.Close();
            else
                _br.Close();
        }

        public void AddDirectory(DirectoryInfo di, string basePath)
        {
            _bw.Write(new TarHeader(di, basePath).Bytes);
            foreach (DirectoryInfo d in di.GetDirectories())
                AddDirectory(d, basePath);
            foreach (FileInfo fi in di.GetFiles())
                AddFile(fi, basePath);
        }

        public void AddDirectory(IDirectoryFolder di, string basePath) {
            _bw.Write(new TarHeader(di, basePath).Bytes);
            foreach (IDirectoryFolder d in di.Folders)
                AddDirectory(d, basePath);
            foreach (IDirectoryFile fi in di.Files)
                AddFile(fi, basePath);
        }

        public void AddFile(IDirectoryFile fi, string basePath)
        {
            _bw.Write(new TarHeader(fi, basePath).Bytes);
            BinaryReader br = new BinaryReader(fi.ContentStream);
            while (br.BaseStream.Position < br.BaseStream.Length)
                _bw.Write(br.ReadBytes(1024));
            AlignTo512(br.BaseStream.Length, false);
        }

        public void AddFile(FileInfo fi, string basePath)
        {
            _bw.Write(new TarHeader(fi, basePath).Bytes);
            BinaryReader br = new BinaryReader(fi.Open(FileMode.Open, FileAccess.Read, FileShare.Read));
            while (br.BaseStream.Position < br.BaseStream.Length)
                _bw.Write(br.ReadBytes(1024));
            AlignTo512(br.BaseStream.Length, false);
        }

        public void AddFile(string name, byte[] data)
        {
            _bw.Write(new TarHeader(name, data).Bytes);
            _bw.Write(data);
            AlignTo512(data.Length, false);
        }
    }
}
