using CassiniDev;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace DotNetTestkit
{
    public class EmbeddedServer: IDisposable
    {
        private Server server;
        private static Random random = new Random(DateTime.Now.Millisecond);
        private readonly Uri baseUrl;

        private EmbeddedServer(Server server)
        {
            this.server = server;
            this.baseUrl = new Uri(server.RootUrl);
        }

        public class Builder
        {
            private int port;
            private List<DirectoryMapping> virtualDirectories = new List<DirectoryMapping>();
            private TextWriter outputWriter;

            protected internal Builder(int port)
            {
                this.port = port;
            }

            public Builder WithVirtualDirectory(string virtualPath, string directoryPath)
            {
                virtualDirectories.Add(new DirectoryMapping(virtualPath, directoryPath));

                return this;
            }

            public Builder WithOutputCollectionTo(TextWriter output)
            {
                outputWriter = output;

                return this;
            }

            public EmbeddedServer Start()
            {
                var mainAppVirtualPath = virtualDirectories.First().VirtualPath;
                var mainAppPhysicalPath = virtualDirectories.First().PhysicalPath;

                var dirPath = Path.GetFullPath(mainAppPhysicalPath);

                if (!Directory.Exists(dirPath))
                {
                    throw new DirectoryNotFoundException(dirPath);
                }
                
                var server = new Server(port, mainAppVirtualPath, mainAppPhysicalPath);

                virtualDirectories.Skip(1).ToList().ForEach(additionalMapping =>
                {
                    server.RegisterAdditionalMapping(additionalMapping.VirtualPath, additionalMapping.PhysicalPath);
                });

                server.OutputWriter = outputWriter;

                server.Start();

                AppDomain.CurrentDomain.DomainUnload += (e, a) =>
                {
                    server.Dispose();
                };

                return new EmbeddedServer(server);
            }
        }

        public static Builder NewServer(int port)
        {
            return new Builder(port);
        }

        public void Dispose()
        {
            server.Dispose();
        }

        public static Builder NewServer()
        {
            return NewServer(RandomPort());
        }

        private static int RandomPort()
        {
            var randomPort = random.Next(3000, 6000);

            IPAddress ipAddress = Dns.GetHostEntry("localhost").AddressList[0];

            try
            {
                TcpListener tcpListener = new TcpListener(ipAddress, randomPort);
                tcpListener.Start();
                tcpListener.Stop();

                return randomPort;
            } catch (SocketException)
            {
                return RandomPort();
            }
        }

        public string ResolveUrl(string path)
        {
            return new Uri(baseUrl, path).ToString();
        }
    }

    class DirectoryMapping
    {
        private static string DirSeparator = Path.DirectorySeparatorChar.ToString();

        public DirectoryMapping(string virtualPath, string physicalPath)
        {
            this.VirtualPath = virtualPath;
            this.PhysicalPath = physicalPath.EndsWith(DirSeparator) ? physicalPath : physicalPath + DirSeparator;
        }

        public string PhysicalPath { get; private set; }
        public string VirtualPath { get; private set; }
    }
}
