using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using System;
using System.Threading;
using Utility.Types;

namespace DCL.UserInAppInitializationFlow.StartupOperations
{
    public abstract class StartUpOperationBase : IStartupOperation
    {
        private readonly Func<Exception, EnumResult<TaskError>> createError;
        private readonly string reportCategory;

        protected StartUpOperationBase(string reportCategory = ReportCategory.SCENE_LOADING)
        {
            createError = e => EnumResult<TaskError>.ErrorResult(TaskError.UnexpectedException, $"Exception in {GetType().Name}:\n{e}");
            this.reportCategory = reportCategory;
        }

        public UniTask<EnumResult<TaskError>> ExecuteAsync(AsyncLoadProcessReport report, CancellationToken ct) =>
            InternalExecuteAsync(report, ct).SuppressToResultAsync(reportCategory, createError);

        /// <summary>
        ///     This function is free to throw exceptions
        /// </summary>
        protected abstract UniTask InternalExecuteAsync(AsyncLoadProcessReport report, CancellationToken ct);
    }
}
