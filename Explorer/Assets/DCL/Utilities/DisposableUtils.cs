using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using System;

namespace DCL.Utilities
{
    public static class DisposableUtils
    {
        public const string DEFAULT_EXCEPTION = "'s thrown an exception on disposal.";

        public static void SafeDispose<T>(this T? disposable, ReportData reportData, Func<T, string>? exceptionMessageFactory = null) where T: IDisposable
        {
            exceptionMessageFactory ??= static d => $"{d.GetType()}{DEFAULT_EXCEPTION}";

            try { disposable?.Dispose(); }
            catch (Exception e) { ReportHub.LogException(new Exception(exceptionMessageFactory(disposable!), e), reportData); }
        }

        public static async UniTask SafeDisposeAsync<T>(this T? disposable, ReportData reportData, Func<T, string>? exceptionMessageFactory = null) where T: IUniTaskAsyncDisposable
        {
            exceptionMessageFactory ??= static d => $"{d.GetType()}{DEFAULT_EXCEPTION}";

            if (disposable == null) return;

            try { await disposable.DisposeAsync(); }
            catch (Exception e) { ReportHub.LogException(new Exception(exceptionMessageFactory(disposable!), e), reportData); }
        }
    }
}
