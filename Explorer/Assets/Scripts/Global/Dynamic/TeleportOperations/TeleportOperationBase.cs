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
        private readonly Func<Exception, Result> createError;

        protected TeleportOperationBase()
        {
            createError = e => Result.ErrorResult($"Exception in {GetType().Name}:\n{e}");
        }

        public UniTask<Result> ExecuteAsync(TeleportParams teleportParams, CancellationToken ct) =>
            InternalExecuteAsync(teleportParams, ct).SuppressToResultAsync(ReportCategory.SCENE_LOADING, createError);

        /// <summary>
        ///     This function is free to throw exceptions
        /// </summary>
        protected abstract UniTask InternalExecuteAsync(TeleportParams teleportParams, CancellationToken ct);
    }
}
