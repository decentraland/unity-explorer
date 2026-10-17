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
#if !UNITY_WEBGL
            bool isMainThread = PlayerLoopHelper.IsMainThread;

            if (isMainThread)
                await DCLTask.SwitchToThreadPool();

            return new ExecuteOnThreadPoolScope(forceReturnOnMainThread || isMainThread);
#else
            return new ExecuteOnThreadPoolScope(true);
#endif

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
