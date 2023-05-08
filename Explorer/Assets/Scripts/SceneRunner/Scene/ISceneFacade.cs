using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace SceneRunner.Scene
{
    public interface ISceneFacade : IDisposable
    {
        /// <summary>
        /// Start an update loop with a given FPS
        /// </summary>
        UniTask StartUpdateLoop(int targetFPS, CancellationToken ct);

        void SetTargetFPS(int fps);
    }
}
