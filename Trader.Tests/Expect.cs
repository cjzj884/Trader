using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;

namespace Trader.Tests
{
    public class Expect
    {
        public static T Throw<T>(Action action) where T : Exception
        {
            try
            {
                action();
            }
            catch (T e)
            {
                return e;
            }
            Assert.Fail($"Expection exception of type {typeof(T)}, but no exception was encountered");
            return null;
        }

        public static T ThrowAsync<T>(Func<Task> func) where T : Exception
        {
            try
            {
                func().Wait();
            }
            catch (AggregateException e)
            {
                if (e.InnerException.GetType() == typeof(T))
                {
                    return e.InnerException as T;
                }
                else
                {
                    throw e.InnerException;
                }
            }
            Assert.Fail($"Expection exception of type {typeof(T)}, but no exception was encountered");
            return null;
        }
    }
}
