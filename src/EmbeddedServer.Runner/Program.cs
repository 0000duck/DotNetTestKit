using NDesk.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotNetTestkit.EmbeddedServerRunner
{
    class Program
    {
        class ProgramOptions
        {
            public int Port { get; set; }
            private List<VirtualPathMapping> mappings = new List<VirtualPathMapping>();

            public List<VirtualPathMapping> VirtualPathMappings {
                get
                {                    
                    return mappings;
                }
            }

            public void AddVirtualPathMapping(string mapping)
            {
                mappings.Add(new VirtualPathMapping(mapping));
            }
        }

        class VirtualPathMapping
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

        static void Main(string[] args)
        {
            var options = new ProgramOptions();

            var set = new OptionSet();

            set.Add("p|port=", x => { options.Port = int.Parse(x); });
            set.Add("m|mapping=", x => { options.AddVirtualPathMapping(x); });

            try
            {
                set.Parse(args);

                var serverPrototype = EmbeddedServer.NewServer(options.Port);

                foreach (var mapping in options.VirtualPathMappings)
                {
                    serverPrototype = serverPrototype.WithVirtualDirectory(mapping.VirtualPath, mapping.PhysicalPath);
                }

                serverPrototype.Start();

                Console.ReadLine();
            }
            catch (OptionException e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }
}
 