using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CassiniDev
{
    public class HttpConnectionWrapper: Connection
    {
        private const int MaxChunkLength = 64 * 1024;

        private const int MaxHeaderBytes = 32 * 1024;

        private Connection m_conn;
        private byte[] initialBytes;
        private int bufferIndex = -1;

        public HttpConnectionWrapper(Connection conn, Server server): base(server, null)
        {
            m_conn = conn;
        }

        public string ReadInitialHeader(out string method, out string version)
        {
            // read the first packet (up to 32K)

            var buffers = new List<byte[]>();

            int i = 0;

            bool requestLineRead = false;

            while (i < MaxHeaderBytes && !requestLineRead)
            {
                var size = MaxHeaderBytes - i;
                var buffer = m_conn.ReadRequestBytes(size);

                if (buffer == null || buffer.Length == 0)
                {
                    break;
                }

                i += buffer.Length;

                buffers.Add(buffer);

                for (int j = 0; j < buffer.Length; j++)
                {
                    if (buffer[j] == (byte)'\r')
                    {
                        requestLineRead = true;
                        break;
                    }
                }

            }

            method = null;
            version = null;

            if (i == 0)
            {
                bufferIndex = -1;
                return null;
            }

            initialBytes = new byte[i];

            i = 0;
            foreach (var buffer in buffers)
            {
                Buffer.BlockCopy(buffer, 0, initialBytes, i, buffer.Length);
                i += buffer.Length;
            }

            bufferIndex = 0;

            /*if (_headerBytes != null)
            {
                // previous partial read
                int len = headerBytes.Length + _headerBytes.Length;
                if (len > MaxHeaderBytes)
                    return false;

                var bytes = new byte[len];
                Buffer.BlockCopy(_headerBytes, 0, bytes, 0, _headerBytes.Length);
                Buffer.BlockCopy(headerBytes, 0, bytes, _headerBytes.Length, headerBytes.Length);
                _headerBytes = bytes;
            }
            else
            {
                _headerBytes = headerBytes;
            }

            // start parsing
            _startHeadersOffset = -1;
            _endHeadersOffset = -1;
            _headerByteStrings = new List<ByteString>();*/

            // find the end of headers
            var parser = new CassiniDev.Request.ByteParser(initialBytes);
            CassiniDev.Request.ByteString requestLine = parser.ReadLine();
            CassiniDev.Request.ByteString[] elems = requestLine.Split(' ');

            if (elems == null || elems.Length < 2 || elems.Length > 3)
            {
                WriteErrorAndClose(400);
                return null;
            }

            method = elems[0].GetString();

            CassiniDev.Request.ByteString urlBytes = elems[1];
            var url = urlBytes.GetString();

            version = elems.Length == 3 ? elems[2].GetString() : "HTTP/1.0";

            return url;
        }

        public override void Close()
        {
            m_conn.Close();
        }

        public override bool Connected
        {
            get
            {
                return m_conn.Connected;
            }
        }

        public new Guid Id
        {
            get
            {
                return m_conn.Id;
            }
        }

        public override string LocalIP
        {
            get
            {
                return m_conn.LocalIP;
            }
        }

        public override void LogRequest(string pathTranslated, string url)
        {
            m_conn.LogRequest(pathTranslated, url);
        }

        public override void LogRequestBody(byte[] content)
        {
            m_conn.LogRequestBody(content);
        }

        public override void LogRequestHeaders(string headers)
        {
            m_conn.LogRequestHeaders(headers);
        }

        public override byte[] ReadRequestBytes(int maxBytes)
        {
            if (bufferIndex == -1)
            {
                return m_conn.ReadRequestBytes(maxBytes);
            }

            var size = initialBytes.Length - bufferIndex;

            if (size == 0)
            {
                return m_conn.ReadRequestBytes(maxBytes);
            }

            if (size > maxBytes)
            {
                size = maxBytes;
            }

            byte[] newBuffer = new byte[size];
            Buffer.BlockCopy(initialBytes, bufferIndex, newBuffer, 0, size);

            bufferIndex += size;

            if (bufferIndex == initialBytes.Length)
            {
                bufferIndex = -1;
                initialBytes = null;
            }

            return newBuffer;
        }

        public override string RemoteIP
        {
            get
            {
                return m_conn.RemoteIP;
            }
        }

        public override ServerLog.LogInfo RequestLog
        {
            get
            {
                return m_conn.RequestLog;
            }
        }

        public override ServerLog.LogInfo ResponseLog
        {
            get
            {
                return m_conn.ResponseLog;
            }
        }

        public override int WaitForRequestBytes()
        {
            if (bufferIndex == -1)
            {
                return m_conn.WaitForRequestBytes();
            }

            return initialBytes.Length - bufferIndex;
        }

        public override void Write100Continue()
        {
            m_conn.Write100Continue();
        }

        internal override void Write200Continue()
        {
            m_conn.Write200Continue();
        }

        public override void WriteBody(byte[] data, int offset, int length)
        {
            m_conn.WriteBody(data, offset, length);
        }

        public override void WriteEntireResponseFromFile(string fileName, bool keepAlive)
        {
            m_conn.WriteEntireResponseFromFile(fileName, keepAlive);
        }

        public override void WriteEntireResponseFromString(int statusCode, string extraHeaders, string body, bool keepAlive)
        {
            m_conn.WriteEntireResponseFromString(statusCode, extraHeaders, body, keepAlive);
        }

        public override void WriteErrorAndClose(int statusCode)
        {
            m_conn.WriteErrorAndClose(statusCode);
        }

        public override void WriteErrorAndClose(int statusCode, string message)
        {
            m_conn.WriteErrorAndClose(statusCode, message);
        }

        public override void WriteErrorWithExtraHeadersAndKeepAlive(int statusCode, string extraHeaders)
        {
            m_conn.WriteErrorWithExtraHeadersAndKeepAlive(statusCode, extraHeaders);
        }

        public override void WriteHeaders(int statusCode, string extraHeaders)
        {
            m_conn.WriteHeaders(statusCode, extraHeaders);
        }

        public override bool IsSecure
        {
            get
            {
                return m_conn.IsSecure;
            }
        }
    }
}
