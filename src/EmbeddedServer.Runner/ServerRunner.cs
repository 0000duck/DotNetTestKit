using DotNetTestkit.EnvironmentLifecycle;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Policy;

namespace DotNetTestkit.EmbeddedServerRunner
{
    public class ServerRunner
    {
        private static int domainId = 1;

        public static void RunWith(ProgramOptions options)
        {
            RunWith(options, Console.Out, Console.Error);
        }

        public static void RunWith(ProgramOptions options, TextWriter output, TextWriter error)
        {
            StartEnvironments(options, output, error);
            StartServerWithVirtualMappings(options);
        }

        private static void StartEnvironments(ProgramOptions options, TextWriter output, TextWriter error)
        {
            options.Dlls.ForEach(dllPath => {
                StartForDomain(CreateAppDomainFor(dllPath), dllPath, options.Types, output, error);
            });

            //StartEnvironments(assemblies, options.Types);
        }

        private static void StartForDomain(AppDomain domain, string dllPath, List<string> types, TextWriter output, TextWriter error)
        {
            var starterType = typeof(EnvironmentStarter);
            var starter = (EnvironmentStarter)domain.CreateInstanceAndUnwrap(
                starterType.Assembly.GetName(false).Name, starterType.FullName);

            starter.Setup(dllPath, output, error);

            types.ForEach(typeName => {
                var environment = starter.ForType(typeName);

                if (environment != null)
                {
                    environment.Start();
                }
            });
        }

        private static AppDomain CreateAppDomainFor(string dllPath)
        {
            var curDomain = AppDomain.CurrentDomain;
            var binPath = Path.GetDirectoryName(dllPath);

            var evidence = new Evidence(curDomain.Evidence);
            var setup = new AppDomainSetup();
            var name = NewDomainName();

            setup.ApplicationName = name;
            setup.DynamicBase = curDomain.DynamicDirectory;
            setup.CachePath = curDomain.SetupInformation.CachePath;
            setup.ShadowCopyFiles = curDomain.SetupInformation.ShadowCopyFiles;
            setup.ShadowCopyDirectories = binPath;
            setup.ApplicationBase = binPath;
            //setup.se

            //setup.ConfigurationFile = Path.Combine(dirPath, configFile);
            setup.PrivateBinPath = binPath;

            Console.WriteLine("Starting Environment for {0}", dllPath);

            return AppDomain.CreateDomain(name, evidence, setup);
        }

        private static string NewDomainName()
        {
            return string.Format("Environment-{0}", domainId++);
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

        private static void StartServerWithVirtualMappings(ProgramOptions options)
        {
            if (options.VirtualPathMappings.Count > 0)
            {
                var serverPrototype = EmbeddedServer.NewServer(options.Port);

                foreach (var mapping in options.VirtualPathMappings)
                {
                    Console.WriteLine("Adding {0} -> {1}", mapping.VirtualPath, mapping.PhysicalPath);
                    serverPrototype = serverPrototype.WithVirtualDirectory(mapping.VirtualPath, mapping.PhysicalPath);
                }

                serverPrototype.Start();

                Console.WriteLine("Server listening at {0}", options.Port);
            }
        }

    }
}