using DotNetTestkit.EmbeddedServerRunner;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CassiniDev.Tests
{
    [TestFixture]
    public class EnvironmentLifecycleIT
    {
        SolutionFiles solutionFiles = SolutionFiles.FromPathFile("solution-dir.txt");

        [Test]
        public void StartTheEnvironment()
        {
            var output = new StringWriter();
            var error = new StringWriter();

            ServerRunner.RunWith(
                EnvironmentOptionsTo(
                    dllPath: "Tests\\ExampleApps\\SetUpEnvironmentApp\\bin\\SetUpEnvironmentApp.dll",
                    typeName: "SetUpEnvironmentApp.ExampleEnvironment"),
                output: output,
                error: error);

            Assert.That(output.ToString(), Does.Contain("Started"));
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
