// -----------------------------------------------------------------------
// <copyright file="CoreConfigMapPath.cs" company="">
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
    using System.Configuration;
    using System.Runtime.InteropServices;
    using System.IO;
    using System.Web;

    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    public class ConfigMapPath : MarshalByRefObject, IConfigMapPath
    {
        private string _machineConfigFilename;
        private bool _pathsAreLocal;
        private string _rootWebConfigFilename;
        private string _siteID;
        private string _siteName;
        private WebConfigurationFileMap _webFileMap;

        internal ConfigMapPath(string physicalPath, ConfigurationFileMap fileMap)
            : this(physicalPath, fileMap, true)
        {
        }

        internal ConfigMapPath(string physicalPath, ConfigurationFileMap fileMap, bool pathsAreLocal)
        {
            this._pathsAreLocal = pathsAreLocal;

            var configDir = Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "Config");

            this._machineConfigFilename = Path.Combine(configDir, "machine.config");
            this._rootWebConfigFilename = Path.Combine(configDir, "web.config");

            if (!string.IsNullOrEmpty(fileMap.MachineConfigFilename))
            {
                if (this._pathsAreLocal)
                {
                    this._machineConfigFilename = Path.GetFullPath(fileMap.MachineConfigFilename);
                }
                else
                {
                    this._machineConfigFilename = fileMap.MachineConfigFilename;
                }
            }

            this._webFileMap = fileMap as WebConfigurationFileMap;

            if (this._webFileMap != null)
            {
                this._siteName = "Default Site";
                this._siteID = "1";

                /*if (this._pathsAreLocal)
                {
                    foreach (string str in this._webFileMap.VirtualDirectories)
                    {
                        this._webFileMap.VirtualDirectories[str].va;
                    }
                }*/
                VirtualDirectoryMapping mapping2 = this._webFileMap.VirtualDirectories[null];

                if (mapping2 != null)
                {
                    this._rootWebConfigFilename = Path.Combine(mapping2.PhysicalDirectory, mapping2.ConfigFileBaseName);
                    this._webFileMap.VirtualDirectories.Remove(null);
                }
            }
        }

        public string GetAppPathForPath(string siteID, string path)
        {
            if (!this.IsSiteMatch(siteID))
            {
                return null;
            }

            VirtualDirectoryMapping pathMapping = this.GetPathMapping(path, true);

            if (pathMapping == null)
            {
                return null;
            }

            return pathMapping.VirtualDirectory;
        }

        public void GetDefaultSiteNameAndID(out string siteName, out string siteID)
        {
            siteName = this._siteName;
            siteID = this._siteID;
        }

        public string GetMachineConfigFilename()
        {
            return this._machineConfigFilename;
        }

        public void GetPathConfigFilename(string siteID, string path, out string directory, out string baseName)
        {
            directory = null;
            baseName = null;
            if (this.IsSiteMatch(siteID))
            {
                VirtualDirectoryMapping pathMapping = this.GetPathMapping(path, false);
                if (pathMapping != null)
                {
                    directory = this.GetPhysicalPathForPath(path, pathMapping);
                    if (directory != null)
                    {
                        baseName = pathMapping.ConfigFileBaseName;
                    }
                }
            }
        }

        private VirtualDirectoryMapping GetPathMapping(string path, bool onlyApps)
        {
            if (this._webFileMap == null)
            {
                return null;
            }
            string virtualPathStringNoTrailingSlash = path;

            while (true)
            {
                VirtualDirectoryMapping mapping = this._webFileMap.VirtualDirectories[virtualPathStringNoTrailingSlash];
                if ((mapping != null) && (!onlyApps || mapping.IsAppRoot))
                {
                    return mapping;
                }
                if (virtualPathStringNoTrailingSlash == "/")
                {
                    return null;
                }
                int length = virtualPathStringNoTrailingSlash.LastIndexOf('/');
                if (length == 0)
                {
                    virtualPathStringNoTrailingSlash = "/";
                }
                else
                {
                    virtualPathStringNoTrailingSlash = virtualPathStringNoTrailingSlash.Substring(0, length);
                }
            }
        }

        private string GetPhysicalPathForPath(string path, VirtualDirectoryMapping mapping)
        {
            string physicalDirectory;
            int length = mapping.VirtualDirectory.Length;
            if (path.Length == length)
            {
                physicalDirectory = mapping.PhysicalDirectory;
            }
            else
            {
                string str2;
                if (path[length] == '/')
                {
                    str2 = path.Substring(length + 1);
                }
                else
                {
                    str2 = path.Substring(length);
                }
                str2 = str2.Replace('/', Path.DirectorySeparatorChar);
                physicalDirectory = Path.Combine(mapping.PhysicalDirectory, str2);
            }
            if (this._pathsAreLocal && IsSuspiciousPhysicalPath(physicalDirectory))
            {
                throw new HttpException("Cannot map path");
            }

            return physicalDirectory;
        }

        public string GetRootWebConfigFilename()
        {
            return this._rootWebConfigFilename;
        }

        private bool IsSiteMatch(string site)
        {
            if (!string.IsNullOrEmpty(site) && site.ToLower() != this._siteName.ToLower())
            {
                return site.ToLower() == this._siteID.ToLower();
            }

            return true;
        }

        public string MapPath(string siteID, string path)
        {
            string str;
            string str2;
            this.GetPathConfigFilename(siteID, path, out str, out str2);

            return str;
        }

        public void ResolveSiteArgument(string siteArgument, out string siteName, out string siteID)
        {
            if (this.IsSiteMatch(siteArgument))
            {
                siteName = this._siteName;
                siteID = this._siteID;
            }
            else
            {
                siteName = siteArgument;
                siteID = null;
            }
        }

        private static bool IsSuspiciousPhysicalPath(string physicalPath)
        {
            bool flag;
            if (!IsSuspiciousPhysicalPath(physicalPath, out flag))
            {
                return false;
            }
            if (flag)
            {
                if ((physicalPath.IndexOf('/') >= 0) || (physicalPath.IndexOf(Path.DirectorySeparatorChar + "..", StringComparison.Ordinal) >= 0))
                {
                    return true;
                }
                for (int i = physicalPath.LastIndexOf(Path.DirectorySeparatorChar); i >= 0; i = physicalPath.LastIndexOf(Path.DirectorySeparatorChar, i - 1))
                {
                    if (!IsSuspiciousPhysicalPath(physicalPath.Substring(0, i), out flag))
                    {
                        return false;
                    }
                    if (!flag)
                    {
                        return true;
                    }
                }
            }
            return true;
        }

        private static bool IsSuspiciousPhysicalPath(string physicalPath, out bool pathTooLong)
        {
            bool flag;
            try
            {
                flag = !string.IsNullOrEmpty(physicalPath) && (string.Compare(physicalPath, Path.GetFullPath(physicalPath), StringComparison.OrdinalIgnoreCase) != 0);
                pathTooLong = false;
            }
            catch (PathTooLongException)
            {
                flag = true;
                pathTooLong = true;
            }
            catch (ArgumentException)
            {
                flag = true;
                pathTooLong = true;
            }
            return flag;
        }

        public override object InitializeLifetimeService()
        {
            return null;
        }

    }
}
