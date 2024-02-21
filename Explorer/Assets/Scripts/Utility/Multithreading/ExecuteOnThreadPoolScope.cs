using Cysharp.Threading.Tasks;
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

        public static async UniTask<ExecuteOnThreadPoolScope> NewScope()
        {
            await UniTask.SwitchToThreadPool();
            return new ExecuteOnThreadPoolScope(false);
        }

        public static async UniTask<ExecuteOnThreadPoolScope> NewScopeWithReturnOnMainThread()
        {
            await UniTask.SwitchToThreadPool();
            return new ExecuteOnThreadPoolScope(false);
        }

        public async ValueTask DisposeAsync()
        {
            if (returnOnMainThreadOnDispose)
                await UniTask.SwitchToMainThread();
        }
    }
}
