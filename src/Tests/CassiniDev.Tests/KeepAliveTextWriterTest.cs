using CassiniDev.Core;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CassiniDev.Tests
{
    [TestFixture]
    public class KeepAliveTextWriterTest
    {
        [Test]
        public void WriteLine()
        {
            var output = new StringWriter();
            var writer = new KeepAliveTextWriter(output);

            writer.WriteLine("Hello");

            Assert.That(output.ToString(), Is.EqualTo("Hello\r\n"));
        }
    }
}
