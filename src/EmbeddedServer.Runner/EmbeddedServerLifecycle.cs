using System;
using DotNetTestkit.EnvironmentLifecycle;

namespace DotNetTestkit.EmbeddedServerRunner
{
    public class EmbeddedServerLifecycle : IEnvironmentLifecycle
    {
        private EmbeddedServer server;

        public EmbeddedServerLifecycle(EmbeddedServer server)
        {
            this.server = server;
        }

        public void Reload()
        {
            throw new NotSupportedException();
        }

        public void Start()
        {
            throw new NotSupportedException();
        }

        public void Stop()
        {
            server.Dispose();
        }
    }
}