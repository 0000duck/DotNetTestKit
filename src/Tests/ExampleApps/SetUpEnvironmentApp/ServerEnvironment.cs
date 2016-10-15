using DotNetTestkit;
using DotNetTestkit.EnvironmentLifecycle;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SetUpEnvironmentApp
{
    [SetUpEnvironment(typeof(EmbeddedServerEnvironment))]
    public class ServerEnvironment
    {
    }

    public class EmbeddedServerEnvironment : IEnvironmentLifecycle
    {
        EmbeddedServer.Builder serverBuilder = EmbeddedServer.NewServer(9901)
            .WithVirtualDirectory("/", Path.GetTempPath());

        private EmbeddedServer server;

        public EmbeddedServerEnvironment()
        {
        }

        public void Start()
        {
            Console.WriteLine("Starting");

            try
            {
                this.server = serverBuilder.Start();
                Console.WriteLine("Started");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        public void Reload()
        {
        }

        public void Stop()
        {
            if (server != null)
            {
                server.Dispose();
                Console.WriteLine("Stopped");
            }
        }
    }
}
