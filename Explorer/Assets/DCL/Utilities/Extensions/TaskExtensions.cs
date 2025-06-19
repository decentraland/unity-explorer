using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using System;
using System.Threading.Tasks;
using Utility.Types;

namespace DCL.Utilities.Extensions
{
    public static class TaskExtensions
    {
        /// <summary>
        ///     <inheritdoc cref="UniTaskExtensions.SuppressToResultAsync" />
        /// </summary>
        public static async Task<EnumResult<TaskError>> SuppressToResultAsync(this Task coreOp, ReportData? reportData = null, Func<Exception, EnumResult<TaskError>>? exceptionToResult = null)
        {
            try
            {
                await coreOp;
                return EnumResult<TaskError>.SuccessResult();
            }
            catch (OperationCanceledException) { return EnumResult<TaskError>.CancelledResult(TaskError.Cancelled); }
            catch (Exception e)
            {
                ReportException(e);
                return exceptionToResult?.Invoke(e) ?? EnumResult<TaskError>.ErrorResult(TaskError.UnexpectedException, e.Message ?? $"Empty message: {e.GetType()?.Name}");
            }

            void ReportException(Exception e)
            {
                if (reportData != null)
                    ReportHub.LogException(e, reportData.Value);
            }
        }

        /// <summary>
        ///     Suppresses all exceptions, reports them and converts them to <see cref="Result" />
        /// </summary>
        public static async UniTask<Result<T>> SuppressToResultAsync<T>(this Task<T> coreOp, ReportData? reportData = null, Func<Exception, Result<T>>? exceptionToResult = null)
        {
            try { return Result<T>.SuccessResult(await coreOp); }
            catch (OperationCanceledException) { return Result<T>.CancelledResult(); }
            catch (Exception e)
            {
                ReportException(e);
                return exceptionToResult?.Invoke(e) ?? Result<T>.ErrorResult(e.Message ?? $"Empty message: {e.GetType()?.Name}");
            }

            void ReportException(Exception e)
            {
                if (reportData != null)
                    ReportHub.LogException(e, reportData.Value);
            }
        }
    }
}
