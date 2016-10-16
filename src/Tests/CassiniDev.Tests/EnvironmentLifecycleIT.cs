using DotNetTestkit;
using DotNetTestkit.EmbeddedServerRunner;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace CassiniDev.Tests
{
    [TestFixture]
    public class EnvironmentLifecycleIT
    {
        private StringWriter error;
        private StringWriter output;

        SolutionFiles solutionFiles = SolutionFiles.FromSolutionRoot();
        SimpleHttpClient client = new SimpleHttpClient();

        [SetUp]
        public void SetUp()
        {
            output = new StringWriter();
            error = new StringWriter();
        }

        [Test]
        public void StartSimpleEnvironment()
        {
            ServerRunner.RunWith(
                EnvironmentOptionsTo(
                    dllPath: "Tests\\ExampleApps\\SetUpEnvironmentApp\\bin\\SetUpEnvironmentApp.dll",
                    typeName: "SetUpEnvironmentApp.ExampleEnvironment"),
                output: output,
                error: error);

            Assert.That(output.ToString(), Does.Contain("Started"));
        }

        [Test]
        public void StartServerEnvironment()
        {
            ServerRunner.RunWith(
                EnvironmentOptionsTo(
                    dllPath: "Tests\\ExampleApps\\SetUpEnvironmentApp\\bin\\SetUpEnvironmentApp.dll",
                    typeName: "SetUpEnvironmentApp.ServerEnvironment"));
            
            Assert.That(client.Get("http://localhost:9901/"), Is.Not.Empty);
        }

        private ProgramOptions EnvironmentOptionsTo(string dllPath, string typeName)
        {
            var options = new ProgramOptions();

            options.AddDll(solutionFiles.ResolvePath(dllPath));
            options.AddEnvironmentClass(typeName);

            return options;
        }
    }
}
