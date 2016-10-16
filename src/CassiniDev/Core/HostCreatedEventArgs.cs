using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CassiniDev.Core
{
    public class HostCreatedEventArgs : EventArgs
    {
        private string physicalPath;
        private string virtualPath;

        public HostCreatedEventArgs(string virtualPath, string physicalPath)
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

            set
            {
                physicalPath = value;
            }
        }

        public string VirtualPath
        {
            get
            {
                return virtualPath;
            }

            set
            {
                virtualPath = value;
            }
        }
    }
}
