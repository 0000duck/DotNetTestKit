using DotNetTestkit.EnvironmentLifecycle;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SetUpEnvironmentApp
{
    [SetUpEnvironment(typeof(ExampleEnvironmentLifecycle))]
    public class ExampleEnvironment
    {
    }

    public class ExampleEnvironmentLifecycle: IEnvironmentLifecycle
    {
        public void Reload()
        {
            Console.WriteLine("Reloaded");
        }

        public void Start()
        {
            Console.WriteLine("Started");
        }

        public void Stop()
        {
            Console.WriteLine("Stopped");
        }
    }
}
