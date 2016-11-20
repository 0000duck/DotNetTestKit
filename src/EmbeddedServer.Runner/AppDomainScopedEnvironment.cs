using System;
using DotNetTestkit.EnvironmentLifecycle;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CassiniDev.Core;
using System.Security.Policy;

namespace DotNetTestkit.EmbeddedServerRunner
{
    internal class AppDomainScopedEnvironment: IEnvironmentLifecycle
    {
        private readonly string dllPath;
        private readonly List<string> types;
        private readonly TextWriter output;
        private readonly TextWriter error;
        private static List<TextWriter> openWriters = new List<TextWriter>();
        private AppDomain appDomain;
        private static int domainId = 1;

        public AppDomainScopedEnvironment(string dllPath, List<string> types, TextWriter output, TextWriter error)
        {
            this.dllPath = dllPath;
            this.types = types;
            this.output = output;
            this.error = error;
        }

        public void Start()
        {
            var starterType = typeof(EnvironmentStarter);
            var domain = CreateAppDomainFor(dllPath);
            var starter = (EnvironmentStarter)domain.CreateInstanceAndUnwrap(
                starterType.Assembly.GetName(false).Name, starterType.FullName);

            var outputWriter = new KeepAliveTextWriter(output);
            var errorWriter = new KeepAliveTextWriter(error);

            var assemblyName = starter.Setup(dllPath,
                outputWriter,
                errorWriter);

            openWriters.Add(outputWriter);
            openWriters.Add(errorWriter);

            //domain.UnhandledException += Domain_UnhandledException;

            var environments = types.Select(typeName => {
                var environment = starter.ForType(assemblyName, typeName);

                if (environment != null)
                {
                    environment.Start();
                }

                return environment;
            }).ToList();

            this.appDomain = domain;
        }

        public void Stop()
        {
            if (appDomain == null)
            {
                return;
            }

            AppDomain.Unload(appDomain);
        }

        private static AppDomain CreateAppDomainFor(string dllPath)
        {
            var curDomain = AppDomain.CurrentDomain;
            var binPath = Path.GetDirectoryName(dllPath);
            //var shadowBin = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            //Directory.CreateDirectory(shadowBin);
            //File.WriteAllText(Path.Combine(shadowBin, "hello.txt"), "Hello");

            //Console.WriteLine("Shadow bin at {0}", shadowBin);

            var evidence = new Evidence(curDomain.Evidence);

            //var setup = new AppDomainSetup();
            var name = NewDomainName();

            //setup.ApplicationName = name;
            //setup.DynamicBase = curDomain.DynamicDirectory;
            //setup.CachePath = shadowBin;
            //setup.ShadowCopyDirectories = null;
            //setup.ShadowCopyFiles = "true";
            //setup.ApplicationBase = binPath;
            ////setup.se

            ////setup.ConfigurationFile = Path.Combine(dirPath, configFile);
            //setup.PrivateBinPath = binPath;

            Console.WriteLine("Starting Environment for {0}", dllPath);

            return AppDomain.CreateDomain(name, evidence, binPath, null, true);
        }

        private static string NewDomainName()
        {
            return string.Format("Environment-{0}", domainId++);
        }

        public void Reload()
        {
            Stop();
            Start();
        }
    }
}