using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace CassiniDev.Tests
{
    public class Execution
    {
        public static void Eventually(Action retriable)
        {
            Exception exceptionToThrow = null;

            TryWithCatch(retriable, out exceptionToThrow);

            var startTime = DateTime.Now;

            while (DateTime.Now.Subtract(startTime).TotalMilliseconds < 5000)
            {
                TryWithCatch(retriable, out exceptionToThrow);

                Thread.Sleep(100);
            }

            if (exceptionToThrow != null)
            {
                throw exceptionToThrow;
            }
        }

        public static void TryWithCatch(Action retriable, out Exception exceptionToThrow)
        {
            try
            {
                retriable();
                exceptionToThrow = null;
                return;
            }
            catch (Exception ex)
            {
                exceptionToThrow = ex;
            }
        }
    }
}
