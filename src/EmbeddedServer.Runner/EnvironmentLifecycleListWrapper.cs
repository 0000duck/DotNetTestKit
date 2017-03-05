using System;
using DotNetTestkit.EnvironmentLifecycle;
using System.Collections.Generic;
using System.Linq;

namespace DotNetTestkit.EmbeddedServerRunner
{
    public class EnvironmentLifecycleListWrapper : IEnvironmentLifecycle
    {
        private readonly List<IEnvironmentLifecycle> environments;

        public EnvironmentLifecycleListWrapper(IEnumerable<IEnvironmentLifecycle> environments)
        {
            this.environments = environments.ToList();
        }

        public virtual void Reload()
        {
            ForEachEnv((env) => env.Reload());
        }

        public virtual void Start()
        {
            ForEachEnv((env) => env.Start());
        }

        public virtual void Stop()
        {
            ForEachEnv((env) => env.Stop());
        }

        protected void ForEachEnv(Action<IEnvironmentLifecycle> fn)
        {
            foreach (var env in environments)
            {
                try
                {
                    fn(env);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }
        }
    }
}