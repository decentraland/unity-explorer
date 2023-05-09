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

        /// <summary>
        /// Change the target fps while the scene is running.
        /// It will take effect right after the next update
        /// </summary>
        /// <param name="fps">Target FPS</param>
        void SetTargetFPS(int fps);

        internal UniTask StartScene();

        internal UniTask Tick(float dt);
    }
}
