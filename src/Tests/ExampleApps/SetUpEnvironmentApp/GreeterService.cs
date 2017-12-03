using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestContracts;

namespace SetUpEnvironmentApp
{
    public class GreeterService : IGreeterService
    {
        public string Greet(string name)
        {
            return string.Format("Hello, {0}", name);
        }
    }
}
