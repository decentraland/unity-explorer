using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using Utility.Types;

namespace Global.Dynamic.TeleportOperations
{
    public abstract class TeleportOperationBase : ITeleportOperation
    {
        public async UniTask<Result> ExecuteAsync(TeleportParams teleportParams, CancellationToken ct)
        {
            try
            {
                await ExecuteAsyncInternal(teleportParams, ct);
                return Result.SuccessResult();
            }
            catch (Exception e) { return Result.ErrorResult($"Exception in {GetType().Name}:\n{e}"); }
        }

        /// <summary>
        ///     This function is free to throw exceptions
        /// </summary>
        protected abstract UniTask ExecuteAsyncInternal(TeleportParams teleportParams, CancellationToken ct);
    }
}
