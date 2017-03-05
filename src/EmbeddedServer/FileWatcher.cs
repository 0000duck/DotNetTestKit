using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Concurrent;
using System.Threading;

namespace DotNetTestkit
{
    public class FileWatcher: IDisposable
    {
        private readonly string pattern;
        private readonly List<FileSystemWatcher> watchers = new List<FileSystemWatcher>();
        private readonly BlockingCollection<FileSystemEventArgs> eventQueue = new BlockingCollection<FileSystemEventArgs>();
        private readonly HashSet<string> inexsistantDirs;
        private bool disposed = false;

        private readonly List<BlockingCollection<FileSystemEventArgs>> subscriberQueues = new List<BlockingCollection<FileSystemEventArgs>>();

        public void Subscribe(Action<FileSystemEventArgs> acceptFileSystemArgs)
        {
            var subscriberQueue = SubscribeQueue();

            Task.Factory.StartNew(() =>
            {
                while (!disposed)
                {
                    FileSystemEventArgs args;

                    if (subscriberQueue.TryTake(out args, 100))
                    {
                        try
                        {
                            acceptFileSystemArgs(args);
                        } catch (Exception ex)
                        {
                            Console.WriteLine(ex.ToString());
                        }
                    }
                };
            });
        }

        private readonly Thread inexistantDirWatch;
        private readonly Thread subscriberQueueCopier;

        private BlockingCollection<FileSystemEventArgs> SubscribeQueue()
        {
            var queue = new BlockingCollection<FileSystemEventArgs>();

            lock(subscriberQueues)
            {
                subscriberQueues.Add(queue);
            }

            return queue;
        }

        private FileWatcher(string pattern, List<string> directories)
        {
            this.pattern = pattern;
            this.inexsistantDirs = new HashSet<string>(directories.Where(dir => !Directory.Exists(dir)));

            directories.Where(dir => !inexsistantDirs.Contains(dir)).ToList()
                .ForEach(StartWatching);

            this.inexistantDirWatch = new Thread(() => {
                while (!disposed)
                {
                    foreach (var dir in inexsistantDirs.ToArray())
                    {
                        if (Directory.Exists(dir))
                        {
                            var parent = Path.GetDirectoryName(dir);
                            var name = Path.GetFileName(dir);

                            inexsistantDirs.Remove(dir);

                            StartWatching(dir);
                            OnChanged(this, new FileSystemEventArgs(WatcherChangeTypes.Created, parent, name));
                        }
                    }

                    Thread.Sleep(100);
                }
            });

            inexistantDirWatch.Start();

            this.subscriberQueueCopier = new Thread(() =>
            {
                while (!disposed)
                {
                    FileSystemEventArgs args;

                    if (!eventQueue.TryTake(out args, 100))
                    {
                        continue;
                    }

                    foreach (var queue in subscriberQueues.ToArray())
                    {
                        queue.Add(args);
                    }
                }
            });

            subscriberQueueCopier.Start();
        }

        private void StartWatching(string dir)
        {
            var watcher = new FileSystemWatcher(dir, pattern);

            watcher.Created += OnChanged;
            watcher.Changed += OnChanged;
            watcher.Deleted += OnChanged;

            watcher.EnableRaisingEvents = true;

            watchers.Add(watcher);
        }

        public Task CompleteWhenNoChangesFor(int millis)
        {
            return WithSubscribedQueue(queue =>
            {
                FileSystemEventArgs args;

                while (queue.TryTake(out args, millis)) { };
            });
        }

        private Task<FileSystemEventArgs> CompleteOnFirstChange()
        {
            return WithSubscribedQueue(queue =>
            {
                FileSystemEventArgs args;

                while (!queue.TryTake(out args, 1000)) {};

                return args;
            });
        }

        private Task WithSubscribedQueue(Action<BlockingCollection<FileSystemEventArgs>> fn)
        {
            return WithSubscribedQueue<object>(queue =>
            {
                fn(queue);
                return null;
            });
        }

        private Task<T> WithSubscribedQueue<T>(Func<BlockingCollection<FileSystemEventArgs>, T> fn)
        {
            var subscribedQueue = SubscribeQueue();

            try
            {
                return Task.Factory.StartNew(() => fn(SubscribeQueue()));
            } finally
            {
                UnsubscribeQueue(subscribedQueue);
            }
        }

        private void UnsubscribeQueue(BlockingCollection<FileSystemEventArgs> subscribedQueue)
        {
            lock (subscriberQueues)
            {
                subscriberQueues.Remove(subscribedQueue);
            }
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            eventQueue.Add(e);
        }

        public void Dispose()
        {
            disposed = true;

            watchers.ForEach(watcher =>
            {
                try
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Created -= OnChanged;
                    watcher.Changed -= OnChanged;
                    watcher.Deleted -= OnChanged;

                    watcher.Dispose();
                } catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            });
        }

        public class Builder
        {
            private readonly string pattern;
            private readonly List<string> directories = new List<string>();

            public Builder(string pattern)
            {
                this.pattern = pattern;
            }

            public Builder InDirectory(string directory)
            {
                this.directories.Add(directory);

                return this;
            }

            public void WatchUntilNoChangesFor(int millis, Action whenNoChanges)
            {
                WithFileWatch(pattern, directories, (watcher) =>
                    watcher.CompleteWhenNoChangesFor(millis).ContinueWith(_ => whenNoChanges())
                );
            }
            
            public void WatchUntilFirstChange(Action<FileSystemEventArgs, FileWatcher> onFirstChange)
            {
                WithFileWatch(pattern, directories, (watcher) =>
                    watcher.CompleteOnFirstChange()
                        .ContinueWith(firstChanged => onFirstChange(firstChanged.Result, watcher))
                );
            }

            public FileWatcher Build()
            {
                return new FileWatcher(pattern, directories);
            }

            private static void WithFileWatch(string pattern, List<string> directories, Func<FileWatcher, Task> fn)
            {
                var watcher = new FileWatcher(pattern, directories);

                fn(watcher).ContinueWith(_ => watcher.Dispose());
            }
        }

        public static Builder For(string pattern)
        {
            return new Builder(pattern);
        }
    }
}