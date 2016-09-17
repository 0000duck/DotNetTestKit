using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace CassiniDev.Autodeploy
{
    public class AssemblyAvailableEventArgs: EventArgs
    {
        public AssemblyName Name { get; private set; }

        public AssemblyAvailableEventArgs(AssemblyName name)
        {
            Name = name;
        }
    }
}
