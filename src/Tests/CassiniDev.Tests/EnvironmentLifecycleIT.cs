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
    [TestFixture, SingleThreaded]
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

        [Test]
        public void ReloadEnvironmentByChangingBinDir()
        {
            var binSourcePath = "Tests\\ExampleApps\\SetUpEnvironmentApp\\bin\\";

            GivenDLLsAreDeployedFrom(binSourcePath, (binDirPath) =>
            {

                using (var env = ServerRunner.RunWith(
                    EnvironmentOptionsTo(
                        dllPath: Path.Combine(binDirPath, "SetUpEnvironmentApp.dll"),
                        typeName: "SetUpEnvironmentApp.ServerEnvironment")))
                {

                    Assert.That(client.Get("http://localhost:9901/"), Is.Not.Empty);

                    EmptyDir(binDirPath);

                    Eventually(() =>
                    {
                        try
                        {
                            Assert.That(client.Get("http://localhost:9901/"), Is.Not.Empty);
                            Assert.Fail("Server must go down");
                        } catch (AssertionException)
                        {
                            throw;
                        } catch (Exception ex)
                        {
                        }
                    });
                }
            });
        }

        [Test]
        public void LoadEnvironmentByPostPopulatingTheDir()
        {
            var binSourcePath = "Tests\\ExampleApps\\SetUpEnvironmentApp\\bin\\";
            var binDirPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            using (var env = ServerRunner.RunWith(
                EnvironmentOptionsTo(
                    dllPath: Path.Combine(binDirPath, "SetUpEnvironmentApp.dll"),
                    typeName: "SetUpEnvironmentApp.ServerEnvironment")))
            {

                CopyFilesTo(binSourcePath, binDirPath);

                Eventually(() => Assert.That(client.Get("http://localhost:9901/"), Is.Not.Empty));
            }
        }

        private void EmptyDir(string targetDir)
        {
            foreach (var file in Directory.GetFiles(targetDir))
            {
                Console.WriteLine("Deleting {0}", file);
                File.Delete(file);
            }
        }

        private void GivenDLLsAreDeployedFrom(string relativePath, Action<string> fun)
        {
            var deployDirPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var deployDir = Directory.CreateDirectory(deployDirPath);

            CopyFilesTo(relativePath, deployDir.FullName);

            try
            {
                fun(deployDirPath);
            }
            finally
            {
                Console.WriteLine("Clearing {0}", deployDirPath);
                deployDir.Delete(true);
            }
        }

        private void CopyFilesTo(string relativePath, string deployDirPath)
        {
            var sourcePath = solutionFiles.ResolvePath(relativePath);

            foreach (var file in Directory.GetFiles(sourcePath))
            {
                var filename = Path.GetFileName(file);

                Console.WriteLine("Copying {0}", filename);

                if (!Directory.Exists(deployDirPath))
                {
                    Directory.CreateDirectory(deployDirPath);
                }
                
                File.Copy(file, Path.Combine(deployDirPath, filename));
            }
        }

        private ProgramOptions EnvironmentOptionsTo(string dllPath, string typeName)
        {
            var options = new ProgramOptions();

            options.AddDll(solutionFiles.ResolvePath(dllPath));
            options.AddEnvironmentClass(typeName);

            return options;
        }


        private void Eventually(Action retriable)
        {
            Exception exceptionToThrow = null;

            TryWithCatch(retriable, out exceptionToThrow);

            var startTime = DateTime.Now;

            while (DateTime.Now.Subtract(startTime).TotalMilliseconds < 5000)
            {
                TryWithCatch(retriable, out exceptionToThrow);

                Thread.Sleep(100);
            }

            if (exceptionToThrow != null)
            {
                throw exceptionToThrow;
            }
        }

        private void TryWithCatch(Action retriable, out Exception exceptionToThrow)
        {
            try
            {
                retriable();
                exceptionToThrow = null;
                return;
            }
            catch (Exception ex)
            {
                exceptionToThrow = ex;
            }
        }
    }
}
