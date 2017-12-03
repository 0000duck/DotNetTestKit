using NUnit.Framework;
using System;
using System.Net;
using System.IO;
using DotNetTestkit;
using System.Linq;
using CassiniDev.Core;
using CassiniDev.Configuration;
using System.CodeDom.Compiler;
using System.Security.AccessControl;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;
using CassiniDev.Deployment;
using TestContracts;

namespace CassiniDev.Tests
{
    [TestFixture]
    public class EmbeddedServerIT
    {
        SimpleHttpClient httpClient = new SimpleHttpClient();
        SolutionFiles solutionFiles = SolutionFiles.FromSolutionRoot();

        [Test]
        public void StartAndStop()
        {
            string pageUrl;

            HostRemovedEventArgs removedArgs = null;

           using (var server = EmbeddedServer.NewServer(1)
                .WithVirtualDirectory("/", solutionFiles.ResolvePath("Tests/CassiniDev4.Tests.Web"))
                .Start())
            {
                HostCreatedEventArgs receivedArgs = null;

                server.HostCreated += (s, e) =>
                {
                    receivedArgs = e;
                };

                server.HostRemoved += (s, e) =>
                {
                    removedArgs = e;
                };

                pageUrl = server.ResolveUrl("Default.aspx");

                Assert.That(httpClient.Get(pageUrl), Does.Contain("Welcome to ASP.NET!"));
                Assert.That(receivedArgs, Is.Not.Null);
                Assert.That(receivedArgs.VirtualPath, Is.EqualTo("/"));
            }

            Assert.That(removedArgs, Is.Not.Null);
            Assert.That(removedArgs.VirtualPath, Is.EqualTo("/"));

            try
            {
                httpClient.Get(pageUrl);

                Assert.Fail("Should not be a success");
            } catch (SimpleHttpClient.UnableToConnect e)
            {
            }
        }

        [Test]
        public void SetupAppWithStart()
        {
            int setupCalled = 0;

            using (var server = EmbeddedServer.NewServer()
                .WithVirtualDirectory("/", solutionFiles.ResolvePath("Tests\\ExampleApps\\RootApp"))
                .WithSetup((serverSetUp) =>
                {
                    setupCalled++;

                    Assert.That(httpClient.Get(serverSetUp.ResolveUrl("Default.aspx")),
                        Does.Contain("Hello, I'm RootApp"));
                })
                .Start())
            {
                Assert.That(httpClient.Get(server.ResolveUrl("Default.aspx")),
                    Does.Contain("Hello, I'm RootApp"));

                Assert.That(setupCalled, Is.EqualTo(1));
            }
        }

        [Test]
        public void ServerBuilderShouldResolveUrl()
        {
            var serverBuilder = EmbeddedServer.NewServer(9999)
                .WithVirtualDirectory("/", solutionFiles.ResolvePath("Tests\\ExampleApps\\RootApp"));

            Assert.That(serverBuilder.ResolveUrl("Default.aspx"),
               Is.EqualTo("http://localhost:9999/Default.aspx"));
        }

        [Test]
        public void LoadMultipleApps()
        {
            var server = EmbeddedServer.NewServer()
                .WithVirtualDirectory("/", solutionFiles.ResolvePath("Tests/ExampleApps/RootApp"))
                .WithVirtualDirectory("/Sub", solutionFiles.ResolvePath("Tests/ExampleApps/SubRootApp"))
                .Start();

            Assert.That(httpClient.Get(server.ResolveUrl("Default.aspx")),
                Does.Contain("Hello, I'm RootApp"));

                Assert.That(httpClient.Get(server.ResolveUrl("Sub/Default.aspx")),
                    Does.Contain("Hello, I'm an appConfig value from RootApp"));
        }

        [Test]
        public void LoadStaticsForSubRootApp()
        {
           var server = EmbeddedServer.NewServer()
                .WithVirtualDirectory("/", solutionFiles.ResolvePath("Tests/ExampleApps/RootApp"))
                .WithVirtualDirectory("/Sub", solutionFiles.ResolvePath("Tests/ExampleApps/SubRootApp"))
                .Start();

            Assert.That(httpClient.Get(server.ResolveUrl("Sub/static/file.js")),
                Does.Contain("function"));
        }

        [Test]
        public void LoadRootAppAtPath()
        {
            var server = EmbeddedServer.NewServer()
                .WithVirtualDirectory("/Sub", solutionFiles.ResolvePath("Tests/ExampleApps/RootApp"))
                .Start();

            Assert.That(httpClient.Get(server.ResolveUrl("Sub/Default.aspx")),
                Does.Contain("Hello, I'm RootApp"));
        }

        [Test]
        public void CollectConsoleOutputFromTheApp()
        {
            var output = new StringWriter();
            var serverPath = solutionFiles.ResolvePath("Tests/ExampleApps/OutputtingApp");

            var server = EmbeddedServer.NewServer()
                 .WithVirtualDirectory("/", serverPath)
                 .WithOutputCollectionTo(output)
                 .Start();

            Assert.That(httpClient.Get(server.ResolveUrl("Default.aspx")),
                Does.Contain("Hello, I'm OutputtingApp"));

            Assert.That(output.ToString().Trim(), Is.EqualTo("Hello!"));
        }

        [Test]
        public void LoadAppWithConfigurationOverwrite()
        {
            var serverPath = solutionFiles.ResolvePath("Tests/ExampleApps/ConfigurableApp");
            var givenConfigRewrite = new ConfigReplacementsBuilder()
                .ForPathWithValues("appSettings", new
                {
                    applicationName = "ConfiguredApp!"
                })
                .Build();

            var server = EmbeddedServer.NewServer()
                 .WithVirtualDirectory("/", new DeployedApp(serverPath, givenConfigRewrite))
                 .Start();

            Assert.That(httpClient.Get(server.ResolveUrl("Default.aspx")),
                Does.Contain("Hello, I'm ConfiguredApp!"));
        }

        [Test]
        [Ignore("WIP")]
        public void GenerateACrossDomainProxyToInjectClass()
        {
            var serverPath = solutionFiles.ResolvePath("Tests/ExampleApps/ConfigurableApp");
            var serviceClass = "A.B.C.SomeClass";

            var spanishGreeter = new SpanishGreeter();

            var givenConfigRewrite = new ConfigReplacementsBuilder()
                .ForPathWithValues("appSettings", new
                {
                    serviceClass = serviceClass
                })
                .Build();

            var server = EmbeddedServer.NewServer()
                 .WithVirtualDirectory("/", new DeployedApp(serverPath, givenConfigRewrite)
                    .WithSyntheticTypeFor<IGreeterService>(serviceClass, spanishGreeter))
                 .Start();

            Assert.That(httpClient.Get(server.ResolveUrl("Default.aspx?Name=Mantas")),
                Does.Contain("Mantas: Hola, Mantas"));
        }

        private string WithRewrittenWebConfig(string projectPath, ConfigReplacements replacements)
        {
            var tempFiles = new AutoRemovableDirectory();
            var root = new DirectoryInfo(projectPath);
            var rootUri = new Uri(Commons.EnsureTrailingSlash(projectPath));

            CopyFilesFromDir(tempFiles, rootUri, root);

            foreach (var dir in root.GetDirectories("*.*", System.IO.SearchOption.AllDirectories))
            {
                CopyFilesFromDir(tempFiles, rootUri, dir);
            }

            RewriteWebConfig(tempFiles, replacements);

            return tempFiles.BasePath;
        }

        private static void RewriteWebConfig(AutoRemovableDirectory tempFiles, ConfigReplacements replacements)
        {
            var webConfig = tempFiles.ReadFile("Web.config");
            var configRewriter = new ConfigRewriter(new ConfigSources(new Dictionary<string, string>()));

            var rewrittenWebConfig = configRewriter.Rewrite(webConfig, replacements);

            tempFiles.WriteFile("Web.config", rewrittenWebConfig);
        }

        private static void CopyFilesFromDir(AutoRemovableDirectory tempFiles, Uri rootUri, DirectoryInfo dir)
        {
            tempFiles.AddDirectory(ToRelativePath(rootUri, Commons.EnsureTrailingSlash(dir.FullName)));

            foreach (var file in dir.GetFiles())
            {
                var fileUri = new Uri(file.FullName);
                var relativeFileUri = rootUri.MakeRelativeUri(fileUri);
                var relativePath = Uri.UnescapeDataString(relativeFileUri.ToString());

                tempFiles.AddFile(relativePath, file.FullName);
            }
        }

        private static string ToRelativePath(Uri rootUri, string fullname)
        {
            var fileUri = new Uri(fullname);
            var relativeFileUri = rootUri.MakeRelativeUri(fileUri);

            return Uri.UnescapeDataString(relativeFileUri.ToString());
        }

        public class SpanishGreeter : IGreeterService
        {
            public string Greet(string name)
            {
                return string.Format("Hola, {0}", name);
            }
        }
    }

    class SimpleHttpClient
    {
        private WebClient webClient = new WebClient();

        public class UnableToConnect: Exception {
            public UnableToConnect(string url) : base("Unable to connect to " + url) { }
        }

        public string Get(string url)
        {
            var request = WebRequest.Create(url);

            try
            {
                var response = (HttpWebResponse)request.GetResponse();

                using (var responseStream = response.GetResponseStream())
                {
                    return new StreamReader(responseStream).ReadToEnd();
                }
            } catch (WebException e) {
                if (e.Response != null)
                {
                    using (var responseStream = e.Response.GetResponseStream())
                    {
                        var error = new StreamReader(responseStream).ReadToEnd();

                        Console.WriteLine(error);
                    }

                    throw e;
                }
                else
                {
                    throw new UnableToConnect(url);
                }                
            }
        }
    }

    public class AutoRemovableDirectory: IDisposable
    {
        private readonly string autoremoveDirectory;
        private readonly int pid;
        private readonly DirectoryInfo processDir;

        public string BasePath { get; internal set; }

        public AutoRemovableDirectory()
        {
            this.pid = Process.GetCurrentProcess().Id;
            this.autoremoveDirectory = Path.Combine(Path.GetTempPath(), ".dotnetteskit-autoremove");
            this.processDir = new DirectoryInfo(Path.Combine(autoremoveDirectory, pid.ToString()));
            this.BasePath = Path.Combine(processDir.FullName, Path.GetRandomFileName());

            AppDomain.CurrentDomain.DomainUnload += CurrentDomain_DomainUnload;
        }

        private void CurrentDomain_DomainUnload(object sender, EventArgs e)
        {
            Dispose();
        }

        public void Dispose()
        {
            if (processDir.Exists)
            {
                processDir.Delete(true);
            }
        }

        public void AddDirectory(string dirName)
        {
            Directory.CreateDirectory(ResolvePath(dirName));
        }

        public void AddFile(string relativePath, string fullName)
        {
            var targetFile = ResolvePath(relativePath);

            File.Copy(fullName, targetFile);
        }

        public FileInfo ResolveFile(string path)
        {
            return new FileInfo(ResolvePath(path));
        }

        public string ReadFile(string path)
        {
            return File.ReadAllText(ResolvePath(path), Encoding.UTF8);
        }

        public void WriteFile(string path, string content)
        {
            File.WriteAllText(ResolvePath(path), content, Encoding.UTF8);
        }

        private string ResolvePath(string relativePath)
        {
            return Path.Combine(BasePath, relativePath);
        }
    }

    internal class Commons
    {
        public static string EnsureTrailingSlash(string path)
        {
            if (path.EndsWith(Path.DirectorySeparatorChar.ToString()) || path.EndsWith(Path.AltDirectorySeparatorChar.ToString()))
            {
                return path;
            }

            return path + Path.DirectorySeparatorChar;
        }
    }
}
