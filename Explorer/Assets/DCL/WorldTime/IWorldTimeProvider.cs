using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace DCL.Time
{
    public interface IWorldTimeProvider : IDisposable
    {
        public UniTask<float> GetWorldTimeAsync(CancellationToken cancellationToken);

        //Not used yet, but in old renderer was used by skybox controller, so there might be an use case
        public void SetPausedState(bool isPaused);
    }
}
