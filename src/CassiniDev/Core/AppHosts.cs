using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Hosting;

namespace CassiniDev
{
    public class AppHosts: MarshalByRefObject
    {
        private Dictionary<string, Host> _hosts = new Dictionary<string, Host>();
        private object _lockObject = new object();

        private bool _shutdownInProgress = false;

        public Server Server { get; internal set; }

        public AppHosts(Server server)
        {
            this.Server = server;
        }

        public Host GetHost(string path)
        {
            if (_shutdownInProgress)
                return null;

            Host host = null;

            string physicalPath;
            string virtualPath = FindApplicationVirtualPath(path, out physicalPath);

            if (!_hosts.TryGetValue(virtualPath, out host))
            {
                lock (_lockObject)
                {
                    if (!_hosts.ContainsKey(virtualPath))
                    {
                        host = CreateHost(physicalPath, virtualPath);

                        _hosts[virtualPath] = host;
                    }
                }
            }
                   
            return host;
        }

        private Host CreateHost(string physicalPath, string virtualPath)
        {
            Host host = new Host();
            host.Configure(_server, _server.Port, virtualPath, physicalPath, _server.RequireAuthentication, _server.DisableDirectoryListing);

            return host;
        }

        private string FindApplicationVirtualPath(string path, out string physicalPath)
        {
            if (path == _virtualPath || path == null)
            {
                physicalPath = _physicalPath;
                return _virtualPath;
            }

            //var entities = new AppSettingsEntities();

            if (_virtualPaths == null)
            {
                //var host = GetHost(_virtualPath);

                //_virtualPaths = host.GetVirtualPaths();
                //_applicationPaths = host.GetApplicationVirtualPaths();
            }

            /*using (var context = new AppSettingsEntities())
            {
                foreach (var app in from app in context.ApplicationSet
                                    where app.PhysicalPath.ToLower() == path
                                    select app)
                {
                    if (_virtualPaths.ContainsKey(app.VirtualPath))
                    {
                        continue;
                    }

                    _virtualPaths.Add(app.VirtualPath, app.PhysicalPath);
                    _applicationPaths.Add(app.VirtualPath);
                }
            }*/

            if (_applicationPaths == null)
            {
                physicalPath = _physicalPath;
                return _virtualPath;
            }

            string bestMatch = null;
            //string bestMatchPhysicalPath = null;

            foreach (var appPath in _applicationPaths)
            {
                if (path.StartsWith(appPath, true, CultureInfo.InvariantCulture) &&
                    (bestMatch == null || appPath.Length > bestMatch.Length))
                {
                    bestMatch = appPath;
                }
            }

            if (bestMatch == null)
            {
                physicalPath = _physicalPath;
                return _virtualPath;
            }

            physicalPath = MapPath(bestMatch);

            return bestMatch;
        }

        internal void HostStopped()
        {
            throw new NotImplementedException();
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="virtualPath"></param>
        /// <param name="physicalPath"></param>
        /// <param name="hostType"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        /// <remarks>
        /// This is Dmitry's hack to enable running outside of GAC.
        /// There are some errors being thrown when running in proc
        /// </remarks>
        private Host CreateWorkerAppDomainWithHost(Host host)
        {
            // create BuildManagerHost in the worker app domain
            //ApplicationManager appManager = ApplicationManager.GetApplicationManager();
            Type buildManagerHostType = typeof(HttpRuntime).Assembly.GetType("System.Web.Compilation.BuildManagerHost");

            IRegisteredObject buildManagerHost = ApplicationManager.CreateObject(host, buildManagerHostType);

            // call BuildManagerHost.RegisterAssembly to make Host type loadable in the worker app domain
            buildManagerHostType.InvokeMember("RegisterAssembly",
                                              BindingFlags.Instance | BindingFlags.InvokeMethod | BindingFlags.NonPublic,
                                              null,
                                              buildManagerHost,
                                              new object[] { host.GetType().Assembly.FullName, host.GetType().Assembly.Location });

            // create Host in the worker app domain
            // FIXME: getting FileLoadException Could not load file or assembly 'WebDev.WebServer20, Version=4.0.1.6, Culture=neutral, PublicKeyToken=f7f6e0b4240c7c27' or one of its dependencies. Failed to grant permission to execute. (Exception from HRESULT: 0x80131418)
            // when running dnoa 3.4 samples - webdev is registering trust somewhere that we are not
            var remoteHost = (Host)ApplicationManager.CreateObject(host, host.GetType());

            remoteHost.Configure(_server, host.Port, host.VirtualPath, host.PhysicalPath, host.RequireAuthentication, host.DisableDirectoryListing);

            return remoteHost;
        }

        internal void AddMapping(string _virtualPath, string _physicalPath)
        {
            throw new NotImplementedException();
        }

        internal IntPtr GetProcessToken()
        {
            return Server.GetProcessToken();
        }

        internal string GetProcessUser()
        {
            return Server.GetProcessUser();
        }

        internal void Shutdown()
        {
            // the host is going to raise an event that this class uses to null the field.
            // just wait until the field is nulled and continue.

            while (_hosts != null)
            {
                new AutoResetEvent(false).WaitOne(100);
            }
        }
    }
}
