using NUnit.Framework;
using System;
using System.Net;
using System.IO;
using DotNetTestkit;
using System.Linq;
using CassiniDev.Core;

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
            string pageUrl;

            HostRemovedEventArgs removedArgs = null;

            using (var server = EmbeddedServer.NewServer()
                .WithVirtualDirectory("/", solutionFiles.ResolvePath("Tests\\CassiniDev4.Tests.Web"))
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
                })
                .Start())
            {
                Assert.That(httpClient.Get(server.ResolveUrl("Default.aspx")),
                    Does.Contain("Hello, I'm RootApp"));

                Assert.That(setupCalled, Is.EqualTo(1));
            }
        }

        [Test]
        public void LoadMultipleApps()
        {
            using (var server = EmbeddedServer.NewServer()
                .WithVirtualDirectory("/", solutionFiles.ResolvePath("Tests\\ExampleApps\\RootApp"))
                .WithVirtualDirectory("/Sub", solutionFiles.ResolvePath("Tests\\ExampleApps\\SubRootApp"))
                .Start())
            {            
                Assert.That(httpClient.Get(server.ResolveUrl("Default.aspx")),
                    Does.Contain("Hello, I'm RootApp"));

                Assert.That(httpClient.Get(server.ResolveUrl("Sub/Default.aspx")),
                    Does.Contain("Hello, I'm an appConfig value from RootApp"));
            }
        }

        [Test]
        public void LoadStaticsForSubRootApp()
        {
            using (var server = EmbeddedServer.NewServer()
                .WithVirtualDirectory("/", solutionFiles.ResolvePath("Tests\\ExampleApps\\RootApp"))
                .WithVirtualDirectory("/Sub", solutionFiles.ResolvePath("Tests\\ExampleApps\\SubRootApp"))
                .Start())
            {
                Assert.That(httpClient.Get(server.ResolveUrl("Sub/static/file.js")),
                    Does.Contain("function"));
            }
        }

        [Test]
        public void LoadRootAppAtPath()
        {
            using (var server = EmbeddedServer.NewServer()
                .WithVirtualDirectory("/Sub", solutionFiles.ResolvePath("Tests\\ExampleApps\\RootApp"))
                .Start())
            {
                Assert.That(httpClient.Get(server.ResolveUrl("Sub/Default.aspx")),
                    Does.Contain("Hello, I'm RootApp"));
            }
        }

        [Test]
        public void CollectConsoleOutputFromTheApp()
        {
            var output = new StringWriter();

            using (var server = EmbeddedServer.NewServer()
                .WithVirtualDirectory("/", solutionFiles.ResolvePath("Tests\\ExampleApps\\OutputtingApp"))
                .WithOutputCollectionTo(output)
                .Start())
            {
                Assert.That(httpClient.Get(server.ResolveUrl("Default.aspx")),
                    Does.Contain("Hello, I'm OutputtingApp"));

                Assert.That(output.ToString().Trim(), Is.EqualTo("Hello!"));
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
}
