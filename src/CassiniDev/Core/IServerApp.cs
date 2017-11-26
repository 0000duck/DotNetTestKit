using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CassiniDev.Core
{
    public interface IServerApp
    {
        IStartedServerApp Start(ServerAppConfiguration appConfig);
    }

    public interface IStartedServerApp: IDisposable
    {
        string PhysicalPath { get; }
    }

    public class SimpleServerApp : IServerApp
    {
        private readonly string physicalPath;

        public SimpleServerApp(string physicalPath)
        {
            this.physicalPath = physicalPath;
        }

        public IStartedServerApp Start(ServerAppConfiguration appConfig)
        {
            return new SimpleStartedApp(physicalPath);
        }
    }

    public class SimpleStartedApp : IStartedServerApp
    {
        public SimpleStartedApp(string physicalPath)
        {
            PhysicalPath = physicalPath;
        }

        public string PhysicalPath
        {
            get; private set;
        }

        public void Dispose()
        {
        }
    }

    public class ServerAppConfiguration
    {
        private readonly string virtualPath;

        public ServerAppConfiguration(string virtualPath)
        {
            this.virtualPath = virtualPath;
        }
    }
}
