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
        private static int domainId = 1;
        private static List<TextWriter> openWriters = new List<TextWriter>();

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

            var outputWriter = new KeepAliveTextWriter(output);
            var errorWriter = new KeepAliveTextWriter(error);

            var assemblyName = starter.Setup(dllPath,
                outputWriter,
                errorWriter);

            openWriters.Add(outputWriter);
            openWriters.Add(errorWriter);

            Console.WriteLine("Started for {0}", dllPath);

            //domain.UnhandledException += Domain_UnhandledException;

            types.ForEach(typeName => {
                var environment = starter.ForType(assemblyName, typeName);

                if (environment != null)
                {
                    environment.Start();
                }
            });
        }

        private static void Domain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Console.WriteLine(e.ExceptionObject.ToString());
        }

        private static AppDomain CreateAppDomainFor(string dllPath)
        {
            var curDomain = AppDomain.CurrentDomain;
            var binPath = Path.GetDirectoryName(dllPath);
            var shadowBin = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            var evidence = new Evidence(curDomain.Evidence);
            var setup = new AppDomainSetup();
            var name = NewDomainName();

            setup.ApplicationName = name;
            setup.DynamicBase = curDomain.DynamicDirectory;
            setup.CachePath = curDomain.SetupInformation.CachePath;
            setup.ShadowCopyDirectories = shadowBin;
            setup.ShadowCopyFiles = "true";
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