using CassiniDev.Misc;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CassiniDev.Tests
{
    [TestFixture]
    public class DynamicForwardingTextWriterIT
    {
        private DynamicForwardingTextWriter forwardingTextWriter;

        [SetUp]
        public void SetUp()
        {
            this.forwardingTextWriter = DynamicForwardingTextWriter.Create();
        }

        [Test]
        public void CollectOutputToWriters()
        {
            var targetStreamA = new StringWriter();
            var targetStreamB = new StringWriter();

            forwardingTextWriter.ForwardTo(targetStreamA);

            forwardingTextWriter.WriteLine("Hello");

            forwardingTextWriter.ForwardTo(targetStreamB);

            forwardingTextWriter.WriteLine("World!");

            Assert.That(targetStreamA.ToString().Trim(), Is.EqualTo("Hello" + Environment.NewLine + "World!"));
            Assert.That(targetStreamB.ToString().Trim(), Is.EqualTo("World!"));
        }
    }
}
