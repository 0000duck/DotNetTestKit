using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CassiniDev.Core
{

    public class KeepAliveTextWriter : StreamWriter
    {
        private TextWriter textWriter;
        private bool running;
        //private readonly Timer flusher;

        public KeepAliveTextWriter(TextWriter textWriter) : this(new CopyingOutputStream(textWriter))
        {
        }

        public KeepAliveTextWriter(Stream stream) : base(stream)
        {
            AutoFlush = true;

            //flusher = new Timer(new TimerCallback((obj) =>
            //{
            //    flus
            //}));

            //flusher.Start();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }

        public override object InitializeLifetimeService()
        {
            return null;
        }
    }

    public class CopyingOutputStream : Stream
    {
        private readonly object bufferStreamLock = new object();
        private MemoryStream bufferStream;
        private TextWriter textWriter;

        public CopyingOutputStream(TextWriter textWriter)
        {
            this.textWriter = textWriter;
            this.bufferStream = new MemoryStream();
        }

        public override bool CanRead
        {
            get
            {
                return false;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return false;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return true;
            }
        }

        public override long Length
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        public override long Position
        {
            get
            {
                throw new NotSupportedException();
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        public override void Flush()
        {
            lock (bufferStreamLock)
            {
                var buffer = bufferStream.ToArray();
                var content = Encoding.Default.GetString(buffer);

                textWriter.Write(content);
                textWriter.Flush();

                bufferStream = new MemoryStream();
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            lock (bufferStreamLock)
            {
                bufferStream.Write(buffer, offset, count);

                byte newLine = (byte)'\n';
                var newLines = Array.FindAll<byte>(buffer, b => b == newLine).ToArray();

                Flush();
            }
        }
    }
}
