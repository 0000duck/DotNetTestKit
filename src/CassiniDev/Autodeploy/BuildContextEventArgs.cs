using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CassiniDev.Autodeploy
{
    public class BuildContextEventArgs: EventArgs
    {
        private BuildContext m_context;

        public BuildContext BuildContext
        {
            get
            {
                return m_context;
            }
        }

        public BuildContextEventArgs(BuildContext context)
        {
            m_context = context;
        }
    }
}
