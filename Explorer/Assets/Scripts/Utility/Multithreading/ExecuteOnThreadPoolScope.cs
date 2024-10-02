using Cysharp.Threading.Tasks;
using System;
using System.Threading.Tasks;

namespace Utility.Multithreading
{
    public readonly struct ExecuteOnThreadPoolScope
    {
        private readonly bool returnOnMainThreadOnDispose;

        private ExecuteOnThreadPoolScope(bool returnOnMainThreadOnDispose)
        {
            this.returnOnMainThreadOnDispose = returnOnMainThreadOnDispose;
        }

        public static async UniTask<ExecuteOnThreadPoolScope> NewScopeAsync()
        {
            await UniTask.SwitchToThreadPool();
            return new ExecuteOnThreadPoolScope(false);
        }

        public static async UniTask<ExecuteOnThreadPoolScope> NewScopeWithReturnOnMainThreadAsync()
        {
            await UniTask.SwitchToThreadPool();
            return new ExecuteOnThreadPoolScope(true);
        }

        public async ValueTask DisposeAsync()
        {
            if (returnOnMainThreadOnDispose)
                await UniTask.SwitchToMainThread();
        }
    }
}
