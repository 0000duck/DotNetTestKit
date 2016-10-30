using CassiniDev.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CassiniDev.Misc
{
    public class DynamicForwardingTextWriter: StreamWriter
    {
        private readonly BroadcastingStream broadcastingStream;

        private DynamicForwardingTextWriter(BroadcastingStream stream) : base(stream)
        {
            this.broadcastingStream = stream;
            AutoFlush = true;
        }

        public static DynamicForwardingTextWriter Create()
        {
            return new DynamicForwardingTextWriter(new BroadcastingStream());
        }

        public static DynamicForwardingTextWriter CreateWith(params TextWriter[] initalOutputs)
        {
            return new DynamicForwardingTextWriter(new BroadcastingStream(initalOutputs));
        }

        public void ForwardTo(TextWriter output)
        {
            broadcastingStream.Add(output);
        }
    }

    public class BroadcastingStream : Stream
    {
        private List<Stream> streams = new List<Stream>();

        public BroadcastingStream(TextWriter[] initalOutputs)
        {
            foreach (var output in initalOutputs)
            {
                Add(output);
            }
        }

        public BroadcastingStream()
        {
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
            ForEachStream(stream => stream.Flush());
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
            ForEachStream(stream => stream.Write(buffer, offset, count));
        }

        public void Add(TextWriter output)
        {
            lock (streams)
            {
                streams.Add(new CopyingOutputStream(output));
            }
        }

        private void ForEachStream(Action<Stream> action)
        {
            foreach (var stream in streams.ToArray())
            {
                action(stream);
            }
        }
    }
}
