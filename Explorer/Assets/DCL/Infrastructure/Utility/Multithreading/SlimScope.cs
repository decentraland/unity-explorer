using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace Utility.Multithreading
{
    public readonly struct SlimScope : IDisposable
    {
        private readonly DCLSemaphoreSlim slim;

        private SlimScope(DCLSemaphoreSlim slim)
        {
            this.slim = slim;
        }

        public static async UniTask<SlimScope> LockAsync(DCLSemaphoreSlim slim)
        {
            await slim.WaitAsync();
            return new SlimScope(slim);
        }

        public void Dispose()
        {
            slim.Release();
        }
    }

    public static class SlimScopeExtensions
    {
        public static UniTask<SlimScope> LockAsync(this DCLSemaphoreSlim slim)
        {
            return SlimScope.LockAsync(slim);
        }
    }
}
