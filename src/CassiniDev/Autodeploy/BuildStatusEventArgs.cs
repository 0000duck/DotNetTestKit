using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CassiniDev.Autodeploy
{
    public class BuildStatusEventArgs: EventArgs
    {
        public BuildStatus BuildStatus
        {
            get;
            internal set;
        }
    }
}
