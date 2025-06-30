using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using System;
using System.Collections.Generic;

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

        public static IEnumerator<Unit> SafeBudgetedDispose<T>(this T? disposable, ReportData reportData, Func<T, string>? exceptionMessageFactory = null) where T: IBudgetedDisposable
        {
            exceptionMessageFactory ??= static d => $"{d.GetType()}{DEFAULT_EXCEPTION}";

            if (disposable == null) yield break;

            IEnumerator<Unit> enumerator = null!;

            try { enumerator = disposable.BudgetedDispose(); }
            catch (Exception e) { ReportHub.LogException(new Exception(exceptionMessageFactory(disposable!)!, e), reportData); }

            // while (true) is justified here
            while (true)
            {
                bool movedNext;

                try { movedNext = enumerator.MoveNext(); }
                catch (Exception e)
                {
                    ReportHub.LogException(new Exception(exceptionMessageFactory(disposable)!, e), reportData);
                    yield break;
                }

                if (!movedNext)
                    yield break;

                yield return enumerator.Current;
            }
        }
    }
}
