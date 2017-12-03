using CassiniDev.Configuration;
using CassiniDev.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace CassiniDev.Deployment
{
    public class DeployedApp : IServerApp
    {
        private readonly string sourcePath;
        private readonly ConfigReplacements replacements;
        private readonly List<ISyntheticType> syntheticTypes = new List<ISyntheticType>();

        public string PhysicalPath
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public DeployedApp(string sourcePath, ConfigReplacements replacements)
        {
            this.sourcePath = sourcePath;
            this.replacements = replacements;
        }

        public IStartedServerApp Start(ServerAppConfiguration appConfig)
        {
            var tempFiles = new AutoRemovableDirectory();
            var root = new DirectoryInfo(sourcePath);
            var rootUri = new Uri(Commons.EnsureTrailingSlash(sourcePath));

            CopyFilesFromDir(tempFiles, rootUri, root);

            foreach (var dir in root.GetDirectories("*.*", SearchOption.AllDirectories))
            {
                CopyFilesFromDir(tempFiles, rootUri, dir);
            }

            RewriteWebConfig(tempFiles, replacements);

            return new DeployedServerApp(tempFiles);
        }

        public DeployedApp WithSyntheticTypeFor<T>(string serviceClass, T implementation)
        {
            syntheticTypes.Add(new SyntheticType<T>(serviceClass, implementation));
            return this;
        }

        private static void RewriteWebConfig(AutoRemovableDirectory tempFiles, ConfigReplacements replacements)
        {
            var webConfig = tempFiles.ReadFile("Web.config");
            var configRewriter = new ConfigRewriter(new ConfigSources(new Dictionary<string, string>()));

            var rewrittenWebConfig = configRewriter.Rewrite(webConfig, replacements);

            tempFiles.WriteFile("Web.config", rewrittenWebConfig);
        }

        private static void CopyFilesFromDir(AutoRemovableDirectory tempFiles, Uri rootUri, DirectoryInfo dir)
        {
            tempFiles.AddDirectory(ToRelativePath(rootUri, Commons.EnsureTrailingSlash(dir.FullName)));

            foreach (var file in dir.GetFiles())
            {
                var fileUri = new Uri(file.FullName);
                var relativeFileUri = rootUri.MakeRelativeUri(fileUri);
                var relativePath = Uri.UnescapeDataString(relativeFileUri.ToString());

                tempFiles.AddFile(relativePath, file.FullName);
            }
        }

        private static string ToRelativePath(Uri rootUri, string fullname)
        {
            var fileUri = new Uri(fullname);
            var relativeFileUri = rootUri.MakeRelativeUri(fileUri);

            return Uri.UnescapeDataString(relativeFileUri.ToString());
        }
    }

    public class DeployedServerApp : IStartedServerApp
    {
        private readonly AutoRemovableDirectory directory;

        public DeployedServerApp(AutoRemovableDirectory dir)
        {
            this.directory = dir;
            this.PhysicalPath = dir.BasePath;
        }

        public string PhysicalPath
        {
            get; private set;
        }

        public void Dispose()
        {
            directory.Dispose();
        }
    }

    public class AutoRemovableDirectory : IDisposable
    {
        private readonly string autoremoveDirectory;
        private readonly int pid;
        private readonly DirectoryInfo processDir;

        public string BasePath { get; internal set; }

        public AutoRemovableDirectory()
        {
            this.pid = Process.GetCurrentProcess().Id;
            this.autoremoveDirectory = Path.Combine(Path.GetTempPath(), ".dotnetteskit-autoremove");
            this.processDir = new DirectoryInfo(Path.Combine(autoremoveDirectory, pid.ToString()));
            this.BasePath = Path.Combine(processDir.FullName, Path.GetRandomFileName());

            AppDomain.CurrentDomain.DomainUnload += CurrentDomain_DomainUnload;
        }

        private void CurrentDomain_DomainUnload(object sender, EventArgs e)
        {
            Dispose();
        }

        public void Dispose()
        {
            if (processDir.Exists)
            {
                processDir.Delete(true);
            }
        }

        public void AddDirectory(string dirName)
        {
            Directory.CreateDirectory(ResolvePath(dirName));
        }

        public void AddFile(string relativePath, string fullName)
        {
            var targetFile = ResolvePath(relativePath);

            File.Copy(fullName, targetFile);
        }

        public FileInfo ResolveFile(string path)
        {
            return new FileInfo(ResolvePath(path));
        }

        public string ReadFile(string path)
        {
            return File.ReadAllText(ResolvePath(path), Encoding.UTF8);
        }

        public void WriteFile(string path, string content)
        {
            File.WriteAllText(ResolvePath(path), content, Encoding.UTF8);
        }

        private string ResolvePath(string relativePath)
        {
            return Path.Combine(BasePath, relativePath);
        }
    }

    internal class Commons
    {
        public static string EnsureTrailingSlash(string path)
        {
            if (path.EndsWith(Path.DirectorySeparatorChar.ToString()) || path.EndsWith(Path.AltDirectorySeparatorChar.ToString()))
            {
                return path;
            }

            return path + Path.DirectorySeparatorChar;
        }
    }

    public interface ISyntheticType
    {
    }

    public class SyntheticType<T>: ISyntheticType
    {
        private readonly T implementation;
        private readonly string typeName;

        public SyntheticType(string typeName, T implementation)
        {
            this.typeName = typeName;
            this.implementation = implementation;
        }
    }
}
