using CassiniDev.Embedded;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Http;
using System.Net;
using System.IO;
using System.Threading;

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
            var server = EmbeddedServer.NewServer(9901)
                .WithVirtualDirectory("/", solutionFiles.ResolvePath("Tests\\CassiniDev4.Tests.Web"))
                .Start();

            try
            {
                Assert.That(httpClient.Get("http://localhost:9901/Default.aspx"),
                Is.StringContaining("Welcome to ASP.NET!"));
            }
            finally
            {
                server.ShutDown();
            }

            try
            {
                httpClient.Get("http://localhost:9901/Default.aspx");

                Assert.Fail("Should not be success");
            } catch (SimpleHttpClient.UnableToConnect e)
            {
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
