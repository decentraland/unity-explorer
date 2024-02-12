using DCL.Diagnostics;
using System;

namespace DCL.Utilities
{
    public static class DisposableUtils
    {
        public const string DEFAULT_EXCEPTION = "'s thrown an exception on disposal.";

        public static void SafeDispose<T>(this T? disposable, ReportData reportData, Func<T, string>? exceptionMessageFactory = null) where T: IDisposable
        {
            static string GetDefaultException(T disposable) =>
                $"{disposable.GetType()}{DEFAULT_EXCEPTION}";

            exceptionMessageFactory ??= GetDefaultException;

            try { disposable?.Dispose(); }
            catch (Exception e) { ReportHub.LogException(new Exception(exceptionMessageFactory(disposable!), e), reportData); }
        }
    }
}
