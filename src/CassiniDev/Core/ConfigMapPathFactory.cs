// -----------------------------------------------------------------------
// <copyright file="CoreConfigMapPathFactory.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

namespace CassiniDev
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Web.Configuration;
    using System.Web;
    using System.Web.Hosting;
    
    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    public class ConfigMapPathFactory : MarshalByRefObject, IConfigMapPathFactory
    {
        private AppHosts _appHosts;

        public ConfigMapPathFactory(AppHosts appHosts)
        {
            _appHosts = appHosts;
        }

        #region IConfigMapPathFactory Members

        //public IConfigMapPath Create(string virtualPath, string physicalPath)
        //{
        //    WebConfigurationFileMap fileMap = new WebConfigurationFileMap();

        //    /*CoreWebsite site = null;

        //    if (host is CoreApplicationHost)
        //    {
        //        site = ((CoreApplicationHost)host).Site;
        //    }

        //    var siteHosts = site.GetApplicationHostsByVirtualPath(virtualPath);*/

        //    fileMap.VirtualDirectories.Add("/dev", new VirtualDirectoryMapping(@"D:\Users\mantasi\Documents\Visual Studio 2010\Projects\Core\Core\", true));
        //    //fileMap.VirtualDirectories.Add("/dev/ismokos", new VirtualDirectoryMapping(@"D:\Users\mantasi\Documents\Visual Studio 2010\Projects\Ismokos\Ismokos\", true));
        //    fileMap.VirtualDirectories.Add(HttpRuntime.AspClientScriptVirtualPath, new VirtualDirectoryMapping(HttpRuntime.AspClientScriptPhysicalPath, false));

        //    return new CoreConfigMapPath(physicalPath, fileMap);
        //}
        public IConfigMapPath Create(string virtualPath, string rawPhysicalPath)
        {
            WebConfigurationFileMap fileMap = new WebConfigurationFileMap();

            var vpLower = virtualPath.TrimEnd('/').ToLower();
            var physicalPath = rawPhysicalPath.TrimEnd('/');

            if (vpLower == String.Empty)
            {
                vpLower = "/";
            }

            var target = vpLower;

            fileMap.VirtualDirectories.Add(vpLower, new VirtualDirectoryMapping(physicalPath, true));

            while ((target = VirtualPathUtility.GetDirectory(target)) != null)
            {
                if (target.Length > 1)
                {
                    target = target.TrimEnd('/');
                }

                var targetPhysicalPath = _appHosts.MapPath(target);

                if (targetPhysicalPath != null)
                {
                    fileMap.VirtualDirectories.Add(target, new VirtualDirectoryMapping(targetPhysicalPath, true));
                }
            }

            fileMap.VirtualDirectories.Add(HttpRuntime.AspClientScriptVirtualPath, new VirtualDirectoryMapping(HttpRuntime.AspClientScriptPhysicalPath, false));

            return new ConfigMapPath(physicalPath, fileMap);
        }

        #endregion

        public override object InitializeLifetimeService()
        {
            return null;
        }
    }
}
