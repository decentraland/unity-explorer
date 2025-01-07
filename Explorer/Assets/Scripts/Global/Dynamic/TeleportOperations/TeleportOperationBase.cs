using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using System;
using System.Threading;
using Utility.Types;

namespace Global.Dynamic.TeleportOperations
{
    public abstract class TeleportOperationBase : ITeleportOperation
    {
        private readonly Func<Exception, EnumResult<TaskError>> createError;

        protected TeleportOperationBase()
        {
            createError = e => EnumResult<TaskError>.ErrorResult(TaskError.UnexpectedException, $"Exception in {GetType().Name}:\n{e}");
        }

        public UniTask<EnumResult<TaskError>> ExecuteAsync(TeleportParams teleportParams, CancellationToken ct) =>
            InternalExecuteAsync(teleportParams, ct).SuppressToResultAsync(ReportCategory.SCENE_LOADING, createError);

        /// <summary>
        ///     This function is free to throw exceptions
        /// </summary>
        protected abstract UniTask InternalExecuteAsync(TeleportParams teleportParams, CancellationToken ct);
    }
}
