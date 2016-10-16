using CassiniDev.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Web;
using System.Web.Hosting;

namespace CassiniDev
{
    public class AppHosts: MarshalByRefObject
    {
        private Dictionary<string, Host> _hosts = new Dictionary<string, Host>();
        private Dictionary<string, string> _mappings = new Dictionary<string, string>();
        private object _lockObject = new object();
        private int _numHosts = 0;

        private bool _shutdownInProgress = false;
        private readonly string _mainAppVirtualPath;
        private readonly string _mainAppPhysicalPath;

        public Server Server { get; internal set; }
        public TextWriter OutputWriter { get; internal set; }

        public readonly ApplicationManager ApplicationManager;

        public event EventHandler<HostCreatedEventArgs> HostCreated;
        public event EventHandler<HostRemovedEventArgs> HostRemoved;

        public AppHosts(Server server, string virtualPath, string physicalPath)
        {
            this._mainAppVirtualPath = virtualPath;
            this._mainAppPhysicalPath = physicalPath;

            this.Server = server;
            this.ApplicationManager = ApplicationManager.GetApplicationManager();

            AddMapping(virtualPath, physicalPath);
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
                lock (_hosts)
                {
                    if (!_hosts.ContainsKey(virtualPath))
                    {
                        host = CreateHost(physicalPath, virtualPath);

                        _hosts[virtualPath] = host;

                        _numHosts++;
                    }
                }
            }
                   
            return host;
        }

        public string MapPath(string virtualPath)
        {
            string bestMatch = null;
            string bestMatchPhysicalPath = null;

            if (virtualPath.StartsWith(_mainAppVirtualPath, true, CultureInfo.InvariantCulture))
            {
                bestMatch = _mainAppVirtualPath;
                bestMatchPhysicalPath = _mainAppPhysicalPath;
            }

            foreach (var pathPair in _mappings)
            {
                var vPath = pathPair.Key;
                var physicalPath = pathPair.Value;

                if (virtualPath.StartsWith(vPath, true, CultureInfo.InvariantCulture) &&
                    (bestMatch == null || vPath.Length > bestMatch.Length))
                {
                    bestMatch = vPath;
                    bestMatchPhysicalPath = physicalPath;
                }
            }

            if (bestMatch != null)
            {
                var sep = Path.DirectorySeparatorChar.ToString();
                var newPath = virtualPath.Substring(bestMatch.Length).Replace("/", sep);
                newPath = newPath.TrimStart(Path.DirectorySeparatorChar);
                newPath = Path.Combine(bestMatchPhysicalPath, newPath);

                return newPath;
            }

            return null;
        }

        public override object InitializeLifetimeService()
        {
            return null;
        }

        private Host CreateHost(string physicalPath, string virtualPath)
        {
            Host host = new Host();
            host.Configure(this, Server.Port, virtualPath, physicalPath, Server.RequireAuthentication, Server.DisableDirectoryListing);

            host = CreateWorkerAppDomainWithHost(host);
            host.SetConsoleOut(OutputWriter ?? Console.Out);

            HostCreated?.Invoke(this, new HostCreatedEventArgs(virtualPath, physicalPath));

            Console.WriteLine("Host created {0}", virtualPath);

            return host;
        }

        private string FindApplicationVirtualPath(string path, out string physicalPath)
        {
            //if (path == _virtualPath || path == null)
            //{
            //    physicalPath = _physicalPath;
            //    return _virtualPath;
            //}

            //var entities = new AppSettingsEntities();

            //if (_virtualPaths == null)
            //{
                //var host = GetHost(_virtualPath);

                //_virtualPaths = host.GetVirtualPaths();
                //_applicationPaths = host.GetApplicationVirtualPaths();
            //}

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

            //if (_applicationPaths == null)
            //{
            //    physicalPath = _physicalPath;
            //    return _virtualPath;
            //}

            string bestMatch = null;
            //string bestMatchPhysicalPath = null;

            foreach (var appPath in _mappings.Keys)
            {
                if (path.StartsWith(appPath, true, CultureInfo.InvariantCulture) &&
                    (bestMatch == null || appPath.Length > bestMatch.Length))
                {
                    bestMatch = appPath;
                }
            }

            if (bestMatch == null)
            {
                physicalPath = _mainAppPhysicalPath;
                return _mainAppVirtualPath;
            }

            physicalPath = MapPath(bestMatch);

            return bestMatch;
        }

        internal void HostStopped(string virtualPath)
        {
            Console.WriteLine("Host stopped {0}", virtualPath);

            if (_hosts.Remove(virtualPath))
            {
                HostRemoved?.Invoke(this, new HostRemovedEventArgs(virtualPath));
                Console.WriteLine("Host removed {0}", virtualPath);
                _numHosts--;
            }
        }

        internal void HostStarted(string virtualPath)
        {
            Console.WriteLine("Host started {0}", virtualPath);            
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

            remoteHost.Configure(this, host.Port, host.VirtualPath, host.PhysicalPath, host.RequireAuthentication, host.DisableDirectoryListing);

            return remoteHost;
        }

        internal void AddMapping(string _virtualPath, string _physicalPath)
        {
            _mappings.Add(_virtualPath, _physicalPath);
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
            Console.WriteLine("Shutting down {0} hosts", _numHosts);

            // the host is going to raise an event that this class uses to null the field.
            // just wait until the field is nulled and continue.
            lock (_hosts)
            {
                var hostKeys = _hosts.Keys.ToList();

                foreach (var hostKey in hostKeys)
                {
                    Console.WriteLine("Shutting down {0}", hostKey);

                    _hosts[hostKey].Shutdown();
                }
            }

            Console.WriteLine("Wait for exit");

            while (_numHosts > 0)
            {
                new AutoResetEvent(false).WaitOne(100);
            }
        }
    }
}
