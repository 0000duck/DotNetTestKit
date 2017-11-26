using CassiniDev;
using CassiniDev.Core;
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
        private readonly List<IDisposable> resources;

        public event EventHandler<HostCreatedEventArgs> HostCreated;
        public event EventHandler<HostRemovedEventArgs> HostRemoved;
        public event EventHandler<HostRemovedEventArgs> HostRemovedWithStableBinDir;

        private EmbeddedServer(Server server, List<IDisposable> resources)
        {
            this.server = server;
            this.baseUrl = new Uri(server.RootUrl);
            this.resources = resources;

            server.HostCreated += (s, e) =>
            {
                if (HostCreated != null)
                {
                    HostCreated(s, e);
                }
            };

            server.HostRemoved += (s, e) =>
            {
                if (HostRemoved != null)
                {
                    HostRemoved(s, e);
                }

                if (HostRemovedWithStableBinDir != null)
                {
                    var binDir = Path.Combine(e.PhysicalPath, "bin");

                    FileWatcher.For("*.dll")
                        .InDirectory(binDir)
                        .WatchUntilNoChangesFor(1000, () =>
                        {
                            HostRemovedWithStableBinDir(this, e);
                        });
                }
            };
        }

        public class ServerSetup
        {
            private readonly int managementPort;
            private readonly Server server;

            public ServerSetup(Server server, int managementPort)
            {
                this.server = server;
                this.managementPort = managementPort;
            }
        }

        public class Builder
        {
            private int port;
            private List<DirectoryMapping> virtualDirectories = new List<DirectoryMapping>();
            private List<Action<ServerSetup>> setupActions = new List<Action<ServerSetup>>();
            private TextWriter outputWriter;

            protected internal Builder(int port)
            {
                this.port = port;
            }

            public Builder WithSetup(Action<ServerSetup> setupAction)
            {
                this.setupActions.Add(setupAction);

                return this;
            }

            public Builder WithVirtualDirectory(string virtualPath, string directoryPath)
            {
                virtualDirectories.Add(new DirectoryMapping(virtualPath, new SimpleServerApp(directoryPath)));

                return this;
            }

            public Builder WithVirtualDirectory(string virtualPath, IServerApp serverApp)
            {
                virtualDirectories.Add(new DirectoryMapping(virtualPath, serverApp));

                return this;
            }

            public Builder WithOutputCollectionTo(TextWriter output)
            {
                outputWriter = output;
                return this;
            }

            public EmbeddedServer Start()
            {
                var virtualDirectory = virtualDirectories.First();

                var mainAppVirtualPath = virtualDirectory.VirtualPath;
                var mainApp = virtualDirectory.ServerApp.Start(new ServerAppConfiguration(virtualDirectory.VirtualPath));
                var mainAppPhysicalPath = mainApp.PhysicalPath;

                var resources = new List<IDisposable>(virtualDirectories.Count);

                resources.Add(mainApp);

                Console.WriteLine("Starting server at {0}", mainAppPhysicalPath);

                string dirPath;

                try
                {
                    dirPath = Path.GetFullPath(mainAppPhysicalPath);
                } catch (Exception e)
                {
                    Console.WriteLine("Failed to starting server at {0}", mainAppPhysicalPath);
                    throw;
                }

                if (!Directory.Exists(dirPath))
                {
                    throw new DirectoryNotFoundException(dirPath);
                }

                Console.WriteLine("Main Virtual Path: {0} -> {1}", mainAppVirtualPath, mainAppPhysicalPath);

                var server = new Server(port, mainAppVirtualPath, mainAppPhysicalPath);

                virtualDirectories.Skip(1).ToList().ForEach(additionalMapping =>
                {
                    var additionalApp = additionalMapping.ServerApp.Start(new ServerAppConfiguration(additionalMapping.VirtualPath));

                    resources.Add(additionalApp);

					Console.WriteLine("Extra Virtual Path: {0} -> {1}", additionalMapping.VirtualPath, additionalApp.PhysicalPath);

					server.RegisterAdditionalMapping(additionalMapping.VirtualPath, additionalApp.PhysicalPath);
                });

                server.OutputWriter = outputWriter;

                server.Start(info =>
                {
                    var serverSetup = new ServerSetup(server, info.Port);

                    foreach (var setupAction in setupActions.ToArray())
                    {
                        setupAction(serverSetup);
                    }
                });

                var embeddedServer = new EmbeddedServer(server, resources);

                AppDomain.CurrentDomain.DomainUnload += (e, a) =>
                {
                    embeddedServer.Dispose();
                };

                return embeddedServer;
            }
        }

        public static Builder NewServer(int port)
        {
            return new Builder(port);
        }

        public void Dispose()
        {
            this.resources.ForEach(resource => {
                try
                {
                    resource.Dispose();
                } catch (Exception e)
                {
                    Console.Error.WriteLine("Error disposing resource {0}", resource.GetType().Name);
                    Console.Error.WriteLine(e);
                }
            });

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

        public DirectoryMapping(string virtualPath, IServerApp serverApp)
        {
            VirtualPath = virtualPath;
            ServerApp = serverApp;
        }

        public string VirtualPath { get; private set; }
        public IServerApp ServerApp { get; private set; }
    }
}
