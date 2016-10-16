using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CassiniDev.Core
{
    public class HostRemovedEventArgs : EventArgs
    {
        private string virtualPath;

        public HostRemovedEventArgs(string virtualPath)
        {
            this.virtualPath = virtualPath;
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
