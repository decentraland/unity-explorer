using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using System;
using Utility;

namespace DCL.Utilities.Extensions
{
    public static class UniTaskExtensions
    {
        public static UniTask<TResult?> SuppressAnyExceptionWithFallback<TResult>(this UniTask<TResult?> coreOp,
            TResult fallbackValue, ReportData? reportData = null) =>
            coreOp.SuppressExceptionWithFallbackAsync(fallbackValue, SuppressExceptionWithFallback.Behaviour.SuppressAnyException, reportData);

        public static async UniTask<TResult?> SuppressExceptionWithFallbackAsync<TResult>(this UniTask<TResult?> coreOp,
            TResult fallbackValue,
            SuppressExceptionWithFallback.Behaviour behaviour = SuppressExceptionWithFallback.Behaviour.Default,
            ReportData? reportData = null)
        {
            try { return await coreOp; }
            catch (UnityWebRequestException e)
            {
                ReportException(e);
                return fallbackValue;
            }
            catch (OperationCanceledException) when (EnumUtils.HasFlag(behaviour, SuppressExceptionWithFallback.Behaviour.SuppressCancellation)) { return fallbackValue; }
            catch (Exception e) when (EnumUtils.HasFlag(behaviour, SuppressExceptionWithFallback.Behaviour.SuppressAnyException))
            {
                ReportException(e);
                return fallbackValue;
            }

            void ReportException(Exception e)
            {
                if (reportData != null)
                    ReportHub.LogException(e, reportData.Value);
            }
        }
    }
}
