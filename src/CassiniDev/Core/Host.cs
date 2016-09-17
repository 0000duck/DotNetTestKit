//  **********************************************************************************
//  CassiniDev - http://cassinidev.codeplex.com
// 
//  Copyright (c) 2010 Sky Sanders. All rights reserved.
//  Copyright (c) Microsoft Corporation. All rights reserved.
//  
//  This source code is subject to terms and conditions of the Microsoft Public
//  License (Ms-PL). A copy of the license can be found in the license.txt file
//  included in this distribution.
//  
//  You must not remove this notice, or any other, from this software.
//  
//  **********************************************************************************

#region

using System;
using System.Globalization;
using System.Security.Permissions;
using System.Security.Principal;
using System.Threading;
using System.Web;
using System.Web.Hosting;
using System.Web.Configuration;
using System.Configuration;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;

#endregion

namespace CassiniDev
{
    /// <summary>
    /// 01/01/10 sky: added HttpRuntime.Close to IRegisteredObject.Stop to eliminate 
    ///               System.AppDomainUnloadedException when running tests in NUnit GuiRunner.
    ///               reference: http://stackoverflow.com/questions/561402/cassini-webserver-webdev-nunit-and-appdomainunloadedexception
    ///               need to test thoroughly but seems to work just fine with no ill effects
    /// 01.03.10 sky: removed the HttpRuntime.Close because, even though it tests fine, I am not entirely certain it is in the right place
    ///               and since I am no longer recommending that the server be used as a library in testing (run a console instance in a new process).
    /// 04.20.11 sky: un-second-guessed myself and un-removed the initial fix. seems to have resolved the issue.
    ///               
    /// </summary>  
    public class Host : MarshalByRefObject, IRegisteredObject, IApplicationHost
    {
        private bool _disableDirectoryListing;

        private string _installPath;

        private string _lowerCasedClientScriptPathWithTrailingSlash;

        private string _lowerCasedVirtualPath;

        private string _lowerCasedVirtualPathWithTrailingSlash;

        private volatile int _pendingCallsCount;

        private string _physicalClientScriptPath;

        private string _physicalPath;

        private int _port;

        private bool _requireAuthentication;

        private AppHosts _appHosts;

        private string _virtualPath;

        public AppDomain AppDomain
        {
            get { return AppDomain.CurrentDomain; }
        }

        public Host()
        {
            HostingEnvironment.RegisterObject(this);
        }

        public bool DisableDirectoryListing
        {
            get { return _disableDirectoryListing; }
        }

        public string InstallPath
        {
            get { return _installPath; }
        }

        public string NormalizedClientScriptPath
        {
            get { return _lowerCasedClientScriptPathWithTrailingSlash; }
        }

        public string NormalizedVirtualPath
        {
            get { return _lowerCasedVirtualPathWithTrailingSlash; }
        }

        public string PhysicalClientScriptPath
        {
            get { return _physicalClientScriptPath; }
        }

        public string PhysicalPath
        {
            get { return _physicalPath; }
        }

        public int Port
        {
            get { return _port; }
        }

        public bool RequireAuthentication
        {
            get { return _requireAuthentication; }
        }

        public string VirtualPath
        {
            get { return _virtualPath; }
        }

        #region IRegisteredObject Members

        void IRegisteredObject.Stop(bool immediate)
        {
            // Unhook the Host so Server will process the requests in the new appdomain.

            if (_appHosts != null)
            {
                _appHosts.HostStopped();
            }

            // Make sure all the pending calls complete before this Object is unregistered.
            WaitForPendingCallsToFinish();

            HostingEnvironment.UnregisterObject(this);

            Thread.Sleep(100);
            HttpRuntime.Close();
            Thread.Sleep(100);
        }

        #endregion

        public void Configure(AppHosts appHosts, int port, string virtualPath, string physicalPath,
                              bool requireAuthentication)
        {
            Configure(appHosts, port, virtualPath, physicalPath, requireAuthentication, false);
        }

        public void Configure(AppHosts appHosts, int port, string virtualPath, string physicalPath)
        {
            Configure(appHosts, port, virtualPath, physicalPath, false, false);
        }

        public void Configure(AppHosts appHosts, int port, string virtualPath, string physicalPath,
                              bool requireAuthentication, bool disableDirectoryListing)
        {
            _appHosts = appHosts;

            _port = port;
            _installPath = null;
            _virtualPath = virtualPath;
            _requireAuthentication = requireAuthentication;
            _disableDirectoryListing = disableDirectoryListing;
            _lowerCasedVirtualPath = CultureInfo.InvariantCulture.TextInfo.ToLower(_virtualPath);
            _lowerCasedVirtualPathWithTrailingSlash = virtualPath.EndsWith("/", StringComparison.Ordinal)
                                                          ? virtualPath
                                                          : virtualPath + "/";
            _lowerCasedVirtualPathWithTrailingSlash =
                CultureInfo.InvariantCulture.TextInfo.ToLower(_lowerCasedVirtualPathWithTrailingSlash);
            _physicalPath = physicalPath;
            _physicalClientScriptPath = HttpRuntime.AspClientScriptPhysicalPath + "\\";
            _lowerCasedClientScriptPathWithTrailingSlash =
                CultureInfo.InvariantCulture.TextInfo.ToLower(HttpRuntime.AspClientScriptVirtualPath + "/");

            //var target = new NLogTarget();

            //NLog.Config.SimpleConfigurator.ConfigureForTargetLogging(target, LogLevel.Debug); 
        }

        public SecurityIdentifier GetProcessSid()
        {
            using (WindowsIdentity identity = new WindowsIdentity(_appHosts.GetProcessToken()))
            {
                return identity.User;
            }
        }

        public IntPtr GetProcessToken()
        {
            new SecurityPermission(PermissionState.Unrestricted).Assert();
            return _appHosts.GetProcessToken();
        }

        public string GetProcessUser()
        {
            return _appHosts.GetProcessUser();
        }

        public override object InitializeLifetimeService()
        {
            // never expire the license
            return null;
        }

        public bool IsVirtualPathAppPath(string path)
        {
            if (path == null)
            {
                return false;
            }
            path = CultureInfo.InvariantCulture.TextInfo.ToLower(path);
            return (path == _lowerCasedVirtualPath || path == _lowerCasedVirtualPathWithTrailingSlash);
        }

        public bool IsVirtualPathInApp(string path, out bool isClientScriptPath)
        {
            isClientScriptPath = false;

            if (path == null)
            {
                return false;
            }

            if (_virtualPath == "/" && path.StartsWith("/", StringComparison.Ordinal))
            {
                if (path.StartsWith(_lowerCasedClientScriptPathWithTrailingSlash, StringComparison.Ordinal))
                {
                    isClientScriptPath = true;
                }
                return true;
            }

            path = CultureInfo.InvariantCulture.TextInfo.ToLower(path);

            if (path.StartsWith(_lowerCasedVirtualPathWithTrailingSlash, StringComparison.Ordinal))
            {
                return true;
            }

            if (path == _lowerCasedVirtualPath)
            {
                return true;
            }

            if (path.StartsWith(_lowerCasedClientScriptPathWithTrailingSlash, StringComparison.Ordinal))
            {
                isClientScriptPath = true;
                return true;
            }

            return false;
        }

        public bool IsVirtualPathInApp(String path)
        {
            bool isClientScriptPath;
            return IsVirtualPathInApp(path, out isClientScriptPath);
        }

        public void ProcessRequest(Connection conn)
        {
            // Add a pending call to make sure our thread doesn't get killed
            AddPendingCall();

            try
            {
                new Request(_appHosts.Server, this, conn).Process();
            }
            finally
            {
                RemovePendingCall();
            }
        }

        [SecurityPermission(SecurityAction.Assert, Unrestricted = true)]
        public void Shutdown()
        {
            HostingEnvironment.InitiateShutdown();
        }

        private void AddPendingCall()
        {
            //TODO: investigate this issue - ref var not volitile
#pragma warning disable 0420
            Interlocked.Increment(ref _pendingCallsCount);
#pragma warning restore 0420
        }

        private void RemovePendingCall()
        {
            //TODO: investigate this issue - ref var not volitile
#pragma warning disable 0420
            Interlocked.Decrement(ref _pendingCallsCount);
#pragma warning restore 0420
        }

        private void WaitForPendingCallsToFinish()
        {
            for (; ; )
            {
                if (_pendingCallsCount <= 0)
                {
                    break;
                }

                Thread.Sleep(250);
            }
        }

        #region IApplicationHost Members

        public IConfigMapPathFactory GetConfigMapPathFactory()
        {
            //var type = typeof(HttpRuntime).Assembly.GetType("System.Web.Hosting.SimpleConfigMapPathFactory");

            //ConstructorInfo ctor = type.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)[0];
            //return (IConfigMapPathFactory)Activator.CreateInstance(type, true); //ctor.Invoke(null, new object[] { GetVirtualPath(), GetPhysicalPath() });

            return new ConfigMapPathFactory(_appHosts);
        }

        public IntPtr GetConfigToken()
        {
            return IntPtr.Zero;
        }

        public string GetPhysicalPath()
        {
            return _physicalPath;
        }

        public string GetSiteID()
        {
            return "1";
        }

        public string GetSiteName()
        {
            return "Default Site";
        }

        public string GetVirtualPath()
        {
            return _virtualPath;
        }

        public void MessageReceived()
        {
        }

        public Dictionary<string, string> GetVirtualPaths()
        {
            var config = (ConfigurationSection)ConfigurationManager.GetSection("virtualPaths");
            var mappings = config.ElementInformation.Properties["mappings"];
            var value = (ConfigurationElementCollection)mappings.Value;

            var res = new Dictionary<string, string>();

            foreach (ConfigurationElement entry in value)
            {
                var vPathProp = entry.ElementInformation.Properties["virtualPath"];
                var pathProp = entry.ElementInformation.Properties["path"];

                res.Add((string)vPathProp.Value, (string)pathProp.Value);
            }

            return res;
        }

        public List<string> GetApplicationVirtualPaths()
        {
            var config = (ConfigurationSection)ConfigurationManager.GetSection("applicationsConfig");
            var mappings = config.ElementInformation.Properties["applications"];
            var value = (ConfigurationElementCollection)mappings.Value;

            var res = new List<string>();

            foreach (ConfigurationElement entry in value)
            {
                var vPathProp = entry.ElementInformation.Properties["applicationPath"];

                res.Add((string)vPathProp.Value);
            }

            return res;
        }

        #endregion

        /*internal void StartAssemblyChangeMonitor()
        {
            var watcher = new FileSystemWatcher(AppDomain.CurrentDomain.SetupInformation.PrivateBinPath, "*.dll");
            watcher.IncludeSubdirectories = false;

            watcher.Changed += (sender, e) =>
            {
                _server.SignalDomainReload(HostingEnvironment.ApplicationHost.GetVirtualPath());
            };

            watcher.Created += (sender, e) =>
            {
                _server.SignalDomainReload(HostingEnvironment.ApplicationHost.GetVirtualPath());
            };

            watcher.Deleted += (sender, e) =>
            {
                _server.SignalDomainReload(HostingEnvironment.ApplicationHost.GetVirtualPath());
            };

            watcher.Renamed += (sender, e) =>
            {
                _server.SignalDomainReload(HostingEnvironment.ApplicationHost.GetVirtualPath());
            };

            watcher.EnableRaisingEvents = true;
        }*/

        internal void UnloadDomain()
        {
            HttpRuntime.UnloadAppDomain();
            //AppDomain.Unload(AppDomain.CurrentDomain);
        }

        internal AppDomain GetAppDomain()
        {
            return AppDomain.CurrentDomain;
        }

        public List<AssemblyName> GetLoadedAssemblies()
        {
            return AppDomain.CurrentDomain.GetAssemblies().Select(x => x.GetName()).ToList();
        }

        /*internal void LoadAssembly(AssemblyName assemblyName)
        {
            var path = new Uri(assemblyName.CodeBase).LocalPath;
            var asm = Assembly.LoadFrom(path);
            BuildManager.AddReferencedAssembly(asm);
        }*/
    }
}