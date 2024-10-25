using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlackEdgeCommon.Utils.Extensions
{
    public static class ActionExtensions
    {
        public static void ExecuteAndCatchException(Action action, Action<Exception> errorDelegate = null, bool logException = false)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                errorDelegate?.Invoke(ex);
                Trace.WriteLine(ex);
            }
        }

        public static TReturn ExecuteWithDefaultIfNull<TInput, TReturn>(Func<TInput, TReturn> func, TInput inputValue, TReturn defaultValue)
            => func == null ? defaultValue : func(inputValue);

        public static TReturn ExecuteWithDefaultIfException<TReturn>(Func<TReturn> func, TReturn defaultValue)
        {
            try
            {
                return func();
            }
            catch
            {
                return defaultValue;
            }
        }

        public static void SafeDispose(this IDisposable disposable)
            => ExecuteAndCatchException(() => disposable?.Dispose());
    }
}
