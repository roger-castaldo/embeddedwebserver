using System;
using System.Collections.Generic;
using System.Text;
using Org.Reddragonit.EmbeddedWebServer.Components.Readers;
using Org.Reddragonit.EmbeddedWebServer.Diagnostics;

namespace Org.Reddragonit.EmbeddedWebServer.Components.Message
{
    internal class HttpParser
    {
        private delegate bool ParserMethod();

        internal delegate void OnRequestLineRecieved(string[] words);
        internal delegate void OnRequestHeaderLineRecieved(string name, string value);
        internal delegate void OnRequestHeaderComplete(bool hasBody, out bool callComplete);
        internal delegate void OnRequestComplete();
        internal delegate void OnBodyBytesRecieved(byte[] buffer, int offset, int count);

        private readonly BufferReader _reader = new BufferReader();
        private int _bodyBytesLeft;
        private byte[] _buffer;
        private string _headerName;
        private string _headerValue;
        private ParserMethod _parserMethod;
        private OnRequestLineRecieved _reqLineRecieved;
        public OnRequestLineRecieved RequestLineRecieved{
            set { _reqLineRecieved = value; }
        }
        private OnRequestHeaderLineRecieved _reqHeaderLineRecieved;
        public OnRequestHeaderLineRecieved RequestHeaderLineRecieved
        {
            set { _reqHeaderLineRecieved=value; }
        }
        private OnRequestHeaderComplete _reqHeaderComplete;
        public OnRequestHeaderComplete RequestHeaderComplete
        {
            set { _reqHeaderComplete = value; }
        }
        private OnBodyBytesRecieved _reqBodyBytesRecieved;
        public OnBodyBytesRecieved RequestBodyBytesRecieved
        {
            set { _reqBodyBytesRecieved = value; }
        }
        private OnRequestComplete _reqComplete;
        public OnRequestComplete RequestComplete
        {
            set { _reqComplete = value; }
        }

        internal HttpParser()
        {
            _parserMethod = ParseRequestLine;
        }

        public int Parse(byte[] buffer, int offset, int count)
        {
            _buffer = buffer;
            _reader.Assign(buffer, offset, count);
            while (_parserMethod())
            {
                Logger.LogMessage(DiagnosticsLevels.TRACE,"Switched parser method to " + _parserMethod.Method.Name + " at index " + _reader.Index);
            }
            return _reader.Index;
        }

        private bool ParseRequestLine()
        {
            _reader.Consume('\r', '\n');

            // Do not contain a complete first line.
            if (!_reader.Contains('\n'))
                return false;

            var words = new string[3];
            words[0] = _reader.ReadUntil(' ');
            _reader.Consume(); // eat delimiter
            words[1] = _reader.ReadUntil(' ');
            _reader.Consume(); // eat delimiter
            words[2] = _reader.ReadLine();
            if (string.IsNullOrEmpty(words[0])
                || string.IsNullOrEmpty(words[1])
                || string.IsNullOrEmpty(words[2]))
                throw new BadRequestException("Invalid request/response line.");
            
            _reqLineRecieved(words);
            _parserMethod = GetHeaderName;
            return true;
        }

        private bool GetHeaderName()
        {
            // empty line. body is begining.
            if (_reader.Current == '\r' && _reader.Peek == '\n')
            {
                // Eat the line break
                _reader.Consume('\r', '\n');
                bool callcomplete = true;
                _reqHeaderComplete(_bodyBytesLeft!=0,out callcomplete);
                // Don't have a body?
                if (_bodyBytesLeft == 0)
                {
                    if (callcomplete)
                        OnComplete();
                    _parserMethod = ParseRequestLine;
                }
                else
                    _parserMethod = GetBody;

                return true;
            }

            _headerName = _reader.ReadUntil(':');
            if (_headerName == null)
                return false;

            _reader.Consume(); // eat colon
            _parserMethod = GetHeaderValue;
            return true;
        }

        private bool GetHeaderValue()
        {
            // remove white spaces.
            _reader.Consume(' ', '\t');

            // multi line or empty value?
            if (_reader.Current == '\r' && _reader.Peek == '\n')
            {
                _reader.Consume('\r', '\n');

                // empty value.
                if (_reader.Current != '\t' && _reader.Current != ' ')
                {
                    _reqHeaderLineRecieved(_headerName, string.Empty);
                    _headerName = null;
                    _headerValue = string.Empty;
                    _parserMethod = GetHeaderName;
                    return true;
                }

                if (_reader.RemainingLength < 1)
                    return false;

                // consume one whitespace
                _reader.Consume();

                // and fetch the rest.
                return GetHeaderValue();
            }

            string value = _reader.ReadLine();
            if (value == null)
                return false;

            _headerValue += value;
            if (string.Compare(_headerName, "Content-Length", true) == 0)
            {
                if (!int.TryParse(value, out _bodyBytesLeft))
                    throw new BadRequestException("Content length is not a number.");
            }

            _reqHeaderLineRecieved(_headerName, value);

            _headerName = null;
            _headerValue = string.Empty;
            _parserMethod = GetHeaderName;
            return true;
        }

        private bool GetBody()
        {
            if (_reader.RemainingLength == 0)
                return false;

            // Got enough bytes to complete body.
            if (_reader.RemainingLength >= _bodyBytesLeft)
            {
                _reqBodyBytesRecieved(_buffer, _reader.Index, _bodyBytesLeft);
                _reader.Index += _bodyBytesLeft;
                _bodyBytesLeft = 0;
                OnComplete();
                return false;
            }

            // eat remaining bytes.
            _reqBodyBytesRecieved(_buffer, _reader.Index, _reader.RemainingLength);
            _bodyBytesLeft -= _reader.RemainingLength;
            _reader.Index = _reader.Length; // place it in the end
            return _reader.Index != _reader.Length;
        }

        private void OnComplete()
        {
            Reset();
            _reqComplete();
        }

        public void Reset()
        {
            _headerValue = null;
            _headerName = string.Empty;
            _bodyBytesLeft = 0;
            _parserMethod = ParseRequestLine;
        }

        public void Clear()
        {
            _reader.Index = 0;
        }
    }
}
