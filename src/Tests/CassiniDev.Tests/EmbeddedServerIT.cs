using NUnit.Framework;
using System;
using System.Net;
using System.IO;
using DotNetTestkit;

namespace CassiniDev.Tests
{
    [TestFixture]
    public class EmbeddedServerIT
    {
        SimpleHttpClient httpClient = new SimpleHttpClient();
        SolutionFiles solutionFiles = SolutionFiles.FromPathFile("solution-dir.txt");

        [Test]
        public void StartAndStop()
        {
            using (var server = EmbeddedServer.NewServer(9901)
                .WithVirtualDirectory("/", solutionFiles.ResolvePath("Tests\\CassiniDev4.Tests.Web"))
                .Start())
            {
                Assert.That(httpClient.Get("http://localhost:9901/Default.aspx"),
                Is.StringContaining("Welcome to ASP.NET!"));
            }

            try
            {
                httpClient.Get("http://localhost:9901/Default.aspx");

                Assert.Fail("Should not be success");
            } catch (SimpleHttpClient.UnableToConnect e)
            {
            }
        }


        [Test]
        public void LoadMultipleApps()
        {
            var server = EmbeddedServer.NewServer(9902)
                .WithVirtualDirectory("/", solutionFiles.ResolvePath("Tests\\ExampleApps\\RootApp"))
                .WithVirtualDirectory("/Sub", solutionFiles.ResolvePath("Tests\\ExampleApps\\SubRootApp"))
                .Start();

            Assert.That(httpClient.Get("http://localhost:9902/Default.aspx"),
                Is.StringContaining("Hello, I'm RootApp"));

            Assert.That(httpClient.Get("http://localhost:9902/Sub/Default.aspx"),
                Is.StringContaining("Hello, I'm an appConfig value from RootApp"));
        }

        [Test]
        public void LoadRootAppAtPath()
        {
            var server = EmbeddedServer.NewServer(9903)
                .WithVirtualDirectory("/Sub", solutionFiles.ResolvePath("Tests\\ExampleApps\\RootApp"))
                .Start();

            Assert.That(httpClient.Get("http://localhost:9903/Sub/Default.aspx"),
                Is.StringContaining("Hello, I'm RootApp"));
        }

        [Test]
        public void CollectConsoleOutputFromTheApp()
        {
            var output = new StringWriter();

            var server = EmbeddedServer.NewServer(9904)
                .WithVirtualDirectory("/", solutionFiles.ResolvePath("Tests\\ExampleApps\\OutputtingApp"))
                .WithOutputCollectionTo(output)
                .Start();

            Assert.That(httpClient.Get("http://localhost:9904/Default.aspx"),
                Is.StringContaining("Hello, I'm OutputtingApp"));

            Assert.That(output.ToString().Trim(), Is.EqualTo("Hello!"));
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

    class SolutionFiles
    {
        private readonly string solutionDir;

        public SolutionFiles(string solutionDir)
        {
            this.solutionDir = solutionDir;
        }

        public static SolutionFiles FromPathFile(string filepath)
        {
            return new SolutionFiles(File.ReadAllText(filepath).Trim());
        }

        internal string ResolvePath(string relativePath)
        {
            return Path.Combine(solutionDir, relativePath);
        }
    }
}
