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
                ReportException(e);
                return exceptionToResult?.Invoke(e) ?? EnumResult<TaskError>.ErrorResult(TaskError.UnexpectedException, e.Message, e);
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
        public static async UniTask<Result<T>> SuppressToResultAsync<T>(this UniTask<T> coreOp, ReportData? reportData = null, Func<Exception, Result<T>>? exceptionToResult = null)
        {
            try { return Result<T>.SuccessResult(await coreOp); }
            catch (OperationCanceledException) { return Result<T>.CancelledResult(); }
            catch (Exception e)
            {
                ReportException(e);
                return exceptionToResult?.Invoke(e) ?? Result<T>.ErrorResult(e.Message);
            }

            void ReportException(Exception e)
            {
                if (reportData != null)
                    ReportHub.LogException(e, reportData.Value);
            }
        }
    }
}
