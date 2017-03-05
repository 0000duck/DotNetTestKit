using DotNetTestkit;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CassiniDev.Tests
{
    [TestFixture]
    public class FileWatcherIT
    {
        private string targetPath;

        [OneTimeSetUp]
        public void SetUp()
        {
            var tempPath = Path.GetTempPath();
            var tempDir = Directory.CreateDirectory(Path.Combine(tempPath, Path.GetRandomFileName()));

            tempDir.Create();

            this.targetPath = tempDir.FullName;
        }

        [Test]
        public void IgnoreEventThatTriggeredBeforeSubscription()
        {
            using (var watcher = FileWatcher.For("*.txt")
                .InDirectory(targetPath)
                .Build())
            {
                HashSet<string> args1 = new HashSet<string>();
                HashSet<string> args2 = new HashSet<string>();

                watcher.Subscribe((ev) => AddCreated(args1, ev));

                File.WriteAllText(Path.Combine(targetPath, "a.txt"), "Hello");

                watcher.CompleteWhenNoChangesFor(100).Wait();

                watcher.Subscribe((ev) => AddCreated(args2, ev));

                File.WriteAllText(Path.Combine(targetPath, "b.txt"), "Hello");

                watcher.CompleteWhenNoChangesFor(100).Wait();

                Execution.Eventually(() =>
                {
                    Assert.That(args1, Does.Contain("a.txt").And.Contain("b.txt"));
                    Assert.That(args2, Does.Contain("b.txt").And.Not.Contain("a.txt"));
                });
            }
        }

        private void AddCreated(HashSet<string> args, FileSystemEventArgs ev)
        {
            if (ev.ChangeType == WatcherChangeTypes.Created)
            {
                args.Add(RelativeTo(targetPath, ev.FullPath));
            }
        }

        private string RelativeTo(string path, string fullPath)
        {
            var dirSep = Path.DirectorySeparatorChar.ToString();

            return new Uri(path.EndsWith(dirSep) ? path : path + dirSep)
                .MakeRelativeUri(new Uri(fullPath)).ToString();
        }
    }
}
