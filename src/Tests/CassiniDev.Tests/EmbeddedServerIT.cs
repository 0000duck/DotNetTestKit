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

        [Test]
        public void StartAndStop()
        {
            var server = EmbeddedServer.NewServer(9901)
                .WithVirtualDirectory("/", "C:\\Code\\CassiniDevMultiApp\\CassiniDev\\src\\Tests\\CassiniDev4.Tests.Web")
                .Start();

            try
            {
                Assert.That(httpClient.Get("http://localhost:9901/Default.aspx"),
                Is.StringContaining("Hello World"));
            }
            finally
            {
                server.ShutDown();
            }

            try
            {
                //httpClient.Get("http://localhost:9901/Default.aspx");

                Assert.Fail("Should not be success");
            } catch (IOException e)
            {
            }
        }
    }

    class SimpleHttpClient
    {
        private WebClient webClient = new WebClient();

        public string Get(string url)
        {
            var request = WebRequest.Create("http://127.0.0.1:9901/Default.aspx");

            try
            {
                var response = (HttpWebResponse)request.GetResponse();

                using (var responseStream = response.GetResponseStream())
                {
                    return new StreamReader(responseStream).ReadToEnd();
                }
            } catch (WebException e) {
                using (var responseStream = e.Response.GetResponseStream())
                {
                    var error = new StreamReader(responseStream).ReadToEnd();

                    Console.WriteLine(error);
                }
                
                throw e;
            }
        }
    }
}
