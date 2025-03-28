using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using System;
using System.Threading;
using Utility.Types;

namespace DCL.RealmNavigation.LoadingOperation
{
    public abstract class LoadingOperationBase<TParams> : ILoadingOperation<TParams> where TParams: ILoadingOperationParams
    {
        private readonly Func<Exception, EnumResult<TaskError>> createError;
        protected readonly string reportCategory;

        protected LoadingOperationBase(string reportCategory = ReportCategory.SCENE_LOADING)
        {
            createError = e => EnumResult<TaskError>.ErrorResult(TaskError.UnexpectedException, $"Exception in {GetType().Name}:\n{e}", e);
            this.reportCategory = reportCategory;
        }

        public UniTask<EnumResult<TaskError>> ExecuteAsync(TParams args, CancellationToken ct) =>
            InternalExecuteAsync(args, ct).SuppressToResultAsync(reportCategory, createError);

        /// <summary>
        ///     This function is free to throw exceptions
        /// </summary>
        protected abstract UniTask InternalExecuteAsync(TParams args, CancellationToken ct);
    }
}
