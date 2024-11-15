using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using System;
using Utility;
using Utility.Types;

namespace DCL.Utilities.Extensions
{
    public static class UniTaskExtensions
    {
        /// <summary>
        ///     Suppresses all exceptions, reports them and converts them to <see cref="Result" />
        /// </summary>
        public static async UniTask<Result> SuppressToResultAsync(this UniTask coreOp, ReportData? reportData = null)
        {
            try
            {
                await coreOp;
                return Result.SuccessResult();
            }
            catch (OperationCanceledException) { return Result.CancelledResult(); }
            catch (Exception e)
            {
                ReportException(e);
                return Result.ErrorResult(e.Message);
            }

            void ReportException(Exception e)
            {
                if (reportData != null)
                    ReportHub.LogException(e, reportData.Value);
            }
        }

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
