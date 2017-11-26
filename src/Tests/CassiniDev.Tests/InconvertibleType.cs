using System;
using System.Runtime.Serialization;

namespace CassiniDev.Tests
{
    [Serializable]
    public class InconvertibleType : Exception
    {
        public InconvertibleType()
        {
        }

        public InconvertibleType(string message) : base(message)
        {
        }

        public InconvertibleType(Type type): this(string.Format("Cannot convert to type {0}", type.Name))
        {
        }

        public InconvertibleType(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected InconvertibleType(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}