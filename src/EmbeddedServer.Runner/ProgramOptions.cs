using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DotNetTestkit.EmbeddedServerRunner
{
    public class ProgramOptions
    {
        public int Port { get; set; }
        private List<VirtualPathMapping> mappings = new List<VirtualPathMapping>();
        private List<string> classes = new List<string>();
        private List<string> dlls = new List<string>();

        public List<VirtualPathMapping> VirtualPathMappings
        {
            get
            {
                return mappings;
            }
        }

        public List<string> Types
        {
            get
            {
                return classes;
            }
        }

        public List<string> Dlls
        {
            get
            {
                return dlls;
            }
        }

        public void AddVirtualPathMapping(string mapping)
        {
            mappings.Add(new VirtualPathMapping(mapping));
        }

        public void AddEnvironmentClass(string className)
        {
            classes.Add(className);
        }

        public void AddDll(string dllPath)
        {
            dlls.Add(dllPath);
        }
    }

    public class VirtualPathMapping
    {
        private readonly string physicalPath;
        private readonly string virtualPath;

        public VirtualPathMapping(string optionValue)
        {
            var split = optionValue.Split(new char[] { ':' }, 2);

            this.virtualPath = split[0];
            this.physicalPath = split[1];
        }

        public string PhysicalPath
        {
            get
            {
                return physicalPath;
            }
        }

        public string VirtualPath
        {
            get
            {
                return virtualPath;
            }
        }
    }
}
