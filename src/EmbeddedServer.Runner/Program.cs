using DotNetTestkit.EnvironmentLifecycle;
using NDesk.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace DotNetTestkit.EmbeddedServerRunner
{
    class Program
    {
        static void Main(string[] args)
        {
            var options = new ProgramOptions();

            var set = new OptionSet();

            try
            {
                set.Add("p|port=", x => { options.Port = int.Parse(x); });
                set.Add("m|mapping=", x => { options.AddVirtualPathMapping(x); });
                set.Add("c|class=", x => { options.AddEnvironmentClass(x); });
                set.Add("<>", x => { options.AddDll(x); });
                set.Parse(args);

                ServerRunner.RunWith(options);

                Console.ReadLine();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }
}
 