using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace Utility.Multithreading
{
    public readonly struct SlimScope : IDisposable
    {
        private readonly SemaphoreSlim slim;

        private SlimScope(SemaphoreSlim slim)
        {
            this.slim = slim;
        }

        public static async UniTask<SlimScope> LockAsync(SemaphoreSlim slim)
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
        public static UniTask<SlimScope> LockAsync(this SemaphoreSlim slim)
        {
            return SlimScope.LockAsync(slim);
        }
    }
}
