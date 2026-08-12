using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utility.Types;
using System;
using UnityEngine.Networking;
using Utility;

namespace DCL.Utilities.Extensions
{
    public static class UniTaskExtensions
    {
        /// <summary>
        ///     Suppresses all exceptions, reports them (doesn't report <see cref="OperationCanceledException"/>) and converts them to <see cref="Result" />
        /// </summary>
        public static async UniTask<EnumResult<TaskError>> SuppressToResultAsync(this UniTask coreOp, ReportData? reportData = null, Func<Exception, EnumResult<TaskError>>? exceptionToResult = null)
        {
            try
            {
                await coreOp;
                return EnumResult<TaskError>.SuccessResult();
            }
            catch (OperationCanceledException)
            {
                return EnumResult<TaskError>.CancelledResult(TaskError.Cancelled);
            }
            catch (Exception e)
            {
                ReportSuppressedException(e, reportData);
                return exceptionToResult?.Invoke(e) ?? EnumResult<TaskError>.ErrorResult(TaskError.UnexpectedException, e.Message, e);
            }
        }

        /// <summary>
        ///     Suppresses all exceptions, reports them and converts them to <see cref="Result" />
        /// </summary>
        public static async UniTask<Result<T>> SuppressToResultAsync<T>(this UniTask<T> coreOp, ReportData? reportData = null, Func<Exception, Result<T>>? exceptionToResult = null)
        {
            try { return Result<T>.SuccessResult(await coreOp); }
            catch (OperationCanceledException) { return Result<T>.CancelledResult(); }
            catch (Exception e)
            {
                ReportSuppressedException(e, reportData);
                return exceptionToResult?.Invoke(e) ?? Result<T>.ErrorResult(e.Message);
            }
        }

        /// <summary>
        ///     Connection-level web request failures (TLS, DNS, unreachable host) are expected transient network
        ///     conditions, so they are reported as warnings; all other exceptions keep full exception reporting
        /// </summary>
        private static void ReportSuppressedException(Exception e, ReportData? reportData)
        {
            if (reportData == null)
                return;

            if (e is UnityWebRequestException { Result: UnityWebRequest.Result.ConnectionError } webRequestException)
                ReportHub.LogWarning(reportData.Value, $"Suppressed web request failure ({webRequestException.Result}, code {webRequestException.ResponseCode}): {webRequestException.Error}");
            else
                ReportHub.LogException(e, reportData.Value);
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
