using ConfigurableApp.Service;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using TestContracts;

namespace ConfigurableApp
{
    public partial class _Default : Page
    {
        public IGreeterService GreeterService;

        public _Default()
        {
            var typeName = ConfigurationManager.AppSettings["serviceClass"];
            var type = Type.GetType(typeName);

            if (type == null)
            {
                throw new Exception(string.Format("Type not found {0}", typeName));
            }

            GreeterService = (IGreeterService)Activator.CreateInstance(type);
        }

        protected void Page_Load(object sender, EventArgs e)
        {

        }
    }
}