using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CassiniDev.Embedded
{
    public static class EmbeddedServer
    {
        public class Builder
        {
            private int port;
            private Dictionary<string, String> virtualDirectories = new Dictionary<string, string>();

            protected internal Builder(int port)
            {
                this.port = port;
            }

            public Builder WithVirtualDirectory(string virtualPath, string directoryPath)
            {
                virtualDirectories.Add(virtualPath, directoryPath);

                return this;
            }

            public Server Start()
            {
                var dirPath = Path.GetFullPath(virtualDirectories.First().Value);

                if (!Directory.Exists(dirPath))
                {
                    throw new DirectoryNotFoundException(dirPath);
                }
                
                var server = new Server(port, virtualDirectories.First().Key, virtualDirectories.First().Value);

                server.Start();

                return server;
            }
        }

        public static Builder NewServer(int port)
        {
            return new Builder(port);
        }
    }
}
