using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CassiniDev.Misc
{
    public static class ObjectValues
    {
        public static Dictionary<string, object> ToDictionary(this object valueForRewrite)
        {
            return ToDictionary<object>(valueForRewrite);
        }

        public static Dictionary<string, T> ToDictionary<T>(this object valueForRewrite)
        {
            var valueType = valueForRewrite.GetType();
            var values = new Dictionary<string, T>();

            foreach (var property in valueType.GetProperties())
            {
                values.Add(property.Name, (T)property.GetValue(valueForRewrite, new object[0]));
            }

            return values;
        }
    }
}
