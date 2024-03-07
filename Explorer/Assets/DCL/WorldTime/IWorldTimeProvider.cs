using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace DCL.Time
{
    public interface IWorldTimeProvider : IDisposable
    {
        public UniTask<float> GetWorldTimeAsync(CancellationToken cancellationToken);
    }
}
