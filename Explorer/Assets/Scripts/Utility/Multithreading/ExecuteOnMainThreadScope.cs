using Cysharp.Threading.Tasks;
using System.Threading.Tasks;

namespace Utility.Multithreading
{
    public readonly struct ExecuteOnMainThreadScope
    {
        private readonly bool returnOnThreadPoolOnDispose;

        private ExecuteOnMainThreadScope(bool returnOnThreadPoolOnDispose)
        {
            this.returnOnThreadPoolOnDispose = returnOnThreadPoolOnDispose;
        }

        public static async UniTask<ExecuteOnMainThreadScope> NewScopeAsync()
        {
            await UniTask.SwitchToMainThread();
            return new ExecuteOnMainThreadScope(false);
        }

        public static async UniTask<ExecuteOnMainThreadScope> NewScopeWithReturnOnThreadPoolAsync()
        {
            await UniTask.SwitchToMainThread();
            return new ExecuteOnMainThreadScope(true);
        }

        public async ValueTask DisposeAsync()
        {
            if (returnOnThreadPoolOnDispose)
                await UniTask.SwitchToThreadPool();
        }
    }
}
