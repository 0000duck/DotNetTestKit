using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using TestContracts;

namespace ConfigurableApp.Service
{
    public class GreeterService: IGreeterService
    {
        public string Greet(string name)
        {
            return string.Format("Hello, {0}", name);
        }
    }
}