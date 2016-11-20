using System;
using System.Collections.Generic;
using DotNetTestkit.EnvironmentLifecycle;

namespace DotNetTestkit.EmbeddedServerRunner
{
    internal class EnvironmentsBinPathWatch : EnvironmentLifecycleListWrapper
    {
        private string binPath;

        public EnvironmentsBinPathWatch(string dllPath, IEnvironmentLifecycle environments) : this(dllPath, new List<IEnvironmentLifecycle> { environments })
        {
        }

        public EnvironmentsBinPathWatch(string dllPath, List<IEnvironmentLifecycle> environments): base(environments)
        {
            this.binPath = dllPath;
        }
        
        public void Start()
        {
            FileWatcher.For("*.dll")
                .InDirectory(binPath)
                .WatchUntilFirstChange((e, watcher) =>
                {
                    Stop();

                    watcher.CompleteWhenNoChangesFor(1000).ContinueWith(_ =>
                    {
                        Start();
                    });
                });

            base.Start();
        }        
    }
}