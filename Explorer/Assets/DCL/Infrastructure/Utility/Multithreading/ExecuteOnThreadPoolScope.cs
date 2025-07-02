using Cysharp.Threading.Tasks;

namespace Utility.Multithreading
{
    public readonly struct ExecuteOnThreadPoolScope
    {
        private readonly bool returnOnMainThreadOnDispose;

        private ExecuteOnThreadPoolScope(bool returnOnMainThreadOnDispose)
        {
            this.returnOnMainThreadOnDispose = returnOnMainThreadOnDispose;
        }

        public static async UniTask<ExecuteOnThreadPoolScope> NewScopeAsync(bool forceReturnOnMainThread = false)
        {
            bool isMainThread = PlayerLoopHelper.IsMainThread;

            if (isMainThread)
                await UniTask.SwitchToThreadPool();

            return new ExecuteOnThreadPoolScope(forceReturnOnMainThread || isMainThread);
        }

        public static async UniTask<ExecuteOnThreadPoolScope> NewScopeWithReturnOnMainThreadAsync() =>
            await NewScopeAsync(true);

        public async UniTask DisposeAsync()
        {
            if (returnOnMainThreadOnDispose)
                await UniTask.SwitchToMainThread();
        }
    }
}
