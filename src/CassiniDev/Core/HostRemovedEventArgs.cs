using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CassiniDev.Core
{
    public class HostRemovedEventArgs : EventArgs
    {
        private readonly string physicalPath;
        private readonly string virtualPath;

        public HostRemovedEventArgs(string virtualPath, string physicalPath)
        {
            this.virtualPath = virtualPath;
            this.physicalPath = physicalPath;
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
