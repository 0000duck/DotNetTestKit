// -----------------------------------------------------------------------
// <copyright file="ApplicationWatcher.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

namespace CassiniDev.Autodeploy
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Web;
    using System.Web.Hosting;
    using System.Reflection;
    using System.Web.Compilation;

    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    public class HostWatch
    {
        private AppDomainSetup m_setupInfo;

#if NET40
        private HostWatchManager m_projMonitor;
#endif

        private ManualResetEvent m_resetEvent = new ManualResetEvent(false);
        private bool m_isLocked;
        private Host m_host;
        private string m_virtualPath;
        private object m_lockObject = new object();
        private Server m_server;
        private static ApplicationManager m_applicationManager = ApplicationManager.GetApplicationManager();
        private Server server;

        public Host Host
        {
            get
            {
                if (m_isLocked)
                {
                    if (!m_resetEvent.WaitOne(60 * 1000))
                    {
                        // TODO respond 503
                    }
                }

                return GetHost(); ;
            }
            set
            {
                m_host = value;
            }
        }

#if NET40
        public HostWatchManager ProjectMonitor
        {
            set
            {
                m_projMonitor = value;
            }
        }
#endif

        public HostWatch(Server server, AppDomain appDomain)
            : this(server, (string)appDomain.GetData(".appVPath"), appDomain.SetupInformation)
        {
        }

        public HostWatch(Server server, string virtualPath, AppDomainSetup setupInfo)
        {
            m_server = server;
            m_virtualPath = virtualPath;
            m_setupInfo = setupInfo;
        }

        public HostWatch(Server server, Host host)
        {
            this.m_server = server;
            this.m_host = host;

            if (host == null)
            {
                return;
            }

            //PrecompileHost(host);

            this.m_setupInfo = host.GetAppDomain().SetupInformation;
            this.m_virtualPath = host.GetVirtualPath();
        }

        public void Start()
        {
#if NET40
            m_projMonitor.OnBuild(m_setupInfo.PrivateBinPath, (sender, e) =>
            {
                m_isLocked = true;
                m_resetEvent.Reset();

                var context = e.BuildContext;

                context.Complete += (sender2, ev) =>
                {
                    if (ev.BuildStatus == BuildStatus.Success)
                    {
                        if (m_host != null)
                        {
                            m_host.UnloadDomain();
                            m_host = null;

                            System.Diagnostics.Debug.WriteLine("Unloading after build");
                        }
                    }

                    //GetHost();

                    m_isLocked = false;
                    m_resetEvent.Set();
                };
            });

            m_projMonitor.OnChanged(m_setupInfo.PrivateBinPath, (sender, e) =>
            {
                if (m_host == null || m_isLocked)
                {
                    return;
                }

                m_host.UnloadDomain();
                m_host = null;

                System.Diagnostics.Debug.WriteLine("Unloading after changed");
            });
#endif
        }

        private Host GetHost()
        {
            if (m_host == null)
            {
                lock (m_lockObject)
                {
                    if (m_host == null)
                    {
                        var host = new Host();
                        host.Configure(m_server, m_server.Port, m_virtualPath, m_setupInfo.ApplicationBase, m_server.RequireAuthentication, m_server.DisableDirectoryListing);

                        m_host = CreateWorkerAppDomainWithHost(host);

                        //PrecompileHost(host);
                    }
                }
            }

            return m_host;
        }

        private void PrecompileHost(CassiniDev.Host host)
        {
            var flags = PrecompilationFlags.ForceDebug |
                  PrecompilationFlags.OverwriteTarget | PrecompilationFlags.Updatable;

            var cbmp = new ClientBuildManagerParameter();
            cbmp.PrecompilationFlags = flags;

            var cbm = new ClientBuildManager(host.VirtualPath, host.PhysicalPath);

            cbm.CompileApplicationDependencies();

            if (!cbm.IsHostCreated)
            {
                return;
            }

            cbm.CompileFile("/Default.aspx");

            System.Diagnostics.Debug.WriteLine("Precompile complete");
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
            IRegisteredObject buildManagerHost = m_applicationManager.CreateObject(host, buildManagerHostType);

            // call BuildManagerHost.RegisterAssembly to make Host type loadable in the worker app domain
            buildManagerHostType.InvokeMember("RegisterAssembly",
                                              BindingFlags.Instance | BindingFlags.InvokeMethod | BindingFlags.NonPublic,
                                              null,
                                              buildManagerHost,
                                              new object[] { host.GetType().Assembly.FullName, host.GetType().Assembly.Location });

            // create Host in the worker app domain
            // FIXME: getting FileLoadException Could not load file or assembly 'WebDev.WebServer20, Version=4.0.1.6, Culture=neutral, PublicKeyToken=f7f6e0b4240c7c27' or one of its dependencies. Failed to grant permission to execute. (Exception from HRESULT: 0x80131418)
            // when running dnoa 3.4 samples - webdev is registering trust somewhere that we are not
            var remoteHost = (Host)m_applicationManager.CreateObject(host, host.GetType());
            remoteHost.Configure(m_server, host.Port, host.VirtualPath, host.PhysicalPath, host.RequireAuthentication, host.DisableDirectoryListing);
            //remoteHost.StartAssemblyChangeMonitor();

            return remoteHost;
        }
    }
}
