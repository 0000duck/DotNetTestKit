using CassiniDev.Core;
using DotNetTestkit.EnvironmentLifecycle;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Policy;
using System.Text;

namespace DotNetTestkit.EmbeddedServerRunner
{
    public class ServerRunner
    {
        public static CombinedEnvironment RunWith(ProgramOptions options)
        {
            return RunWith(options, Console.Out, Console.Error);
        }

        public static CombinedEnvironment RunWith(ProgramOptions options, TextWriter output, TextWriter error)
        {
            var env = new CombinedEnvironment(options, output, error);

            env.Start();

            return env;
        }
    }

    public class CombinedEnvironment: IDisposable
    {
        private TextWriter error;
        private ProgramOptions options;
        private TextWriter output;
        private List<IEnvironmentLifecycle> environments = new List<IEnvironmentLifecycle>();

        public CombinedEnvironment(ProgramOptions options, TextWriter output, TextWriter error)
        {
            this.options = options;
            this.output = output;
            this.error = error;
        }

        internal void Start()
        {
            environments.AddRange(StartEnvironments(options, output, error));
            environments.AddRange(StartServerWithVirtualMappings(options));
        }

        private static IEnumerable<IEnvironmentLifecycle> StartEnvironments(ProgramOptions options, TextWriter output, TextWriter error)
        {
            return options.Dlls.SelectMany(dllPath => {
                return StartForDomain(dllPath, options.Types, output, error);
            });

            //StartEnvironments(assemblies, options.Types);
        }

        private static IEnumerable<IEnvironmentLifecycle> StartForDomain(string dllPath, List<string> types, TextWriter output, TextWriter error)
        {
            var binPath = Path.GetDirectoryName(dllPath);
            var watchedEnvironment = new EnvironmentsBinPathWatch(binPath,
                new AppDomainScopedEnvironment(dllPath, types, output, error));

            watchedEnvironment.Start();

            return new List<IEnvironmentLifecycle> { watchedEnvironment };
        }

        private static void Domain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Console.WriteLine(e.ExceptionObject.ToString());
        }

        private static void StartEnvironments(List<Assembly> assemblies, List<string> types)
        {
            List<IEnvironmentLifecycle> environments = new List<IEnvironmentLifecycle>();

            foreach (var typeName in types)
            {
                Type type = null;
                foreach (var assembly in assemblies)
                {
                    type = assembly.GetType(typeName);

                    if (type != null)
                    {
                        break;
                    }
                }

                if (type == null)
                {
                    Console.WriteLine("ERROR: Failed to load {0}", typeName);
                    return;
                }
                else
                {
                    Console.WriteLine("Loading environments for {0}", typeName);

                    environments.Add(Environments.ForType(type));
                }
            }

            StartEnvironments(environments);
        }

        private static void StartEnvironments(List<IEnvironmentLifecycle> environments)
        {
            foreach (var env in environments)
            {
                Console.WriteLine("Starting {0}", env.GetType().FullName);

                env.Start();
            }
        }

        private static IEnumerable<IEnvironmentLifecycle> StartServerWithVirtualMappings(ProgramOptions options)
        {
            if (options.VirtualPathMappings.Count > 0)
            {
                var serverPrototype = EmbeddedServer.NewServer(options.Port);

                foreach (var mapping in options.VirtualPathMappings)
                {
                    Console.WriteLine("Adding {0} -> {1}", mapping.VirtualPath, mapping.PhysicalPath);
                    serverPrototype = serverPrototype.WithVirtualDirectory(mapping.VirtualPath, mapping.PhysicalPath);
                }

                var server = serverPrototype.Start();

                Console.WriteLine("Server listening at {0}", options.Port);

                return new List<IEnvironmentLifecycle>() {
                    new EmbeddedServerLifecycle(server)
                };
            }

            return new List<IEnvironmentLifecycle>();
        }

        public void Dispose()
        {
            environments.ForEach(env =>
            {
                try
                {
                    env.Stop();
                } catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            });
        }
    }
}