#if UNITY_WEBGL && !UNITY_EDITOR

using SceneRunner.Scene;
using SceneRunner.Scene.ExceptionsHandling;
using System.Collections.Generic;

namespace ECS.SceneLifeCycle.WebGL
{
    /// <summary>
    ///     Queue for WebGL scene updates. Scenes enqueue ticks; main loop drains before scene ECS runs.
    /// </summary>
    public interface IWebGLSceneUpdateQueue
    {
        void Enqueue(ISceneFacade scene, float dt, ISceneExceptionsHandler exceptionHandler);
        void ProcessPendingUpdates();
    }

    public class WebGLSceneUpdateQueue : IWebGLSceneUpdateQueue
    {
        private readonly List<(ISceneFacade Scene, float Dt, ISceneExceptionsHandler ExceptionHandler)> pending = new (32);
        private readonly List<(ISceneFacade Scene, float Dt, ISceneExceptionsHandler ExceptionHandler)> processing = new (32);

        public void Enqueue(ISceneFacade scene, float dt, ISceneExceptionsHandler exceptionHandler)
        {
            pending.Add((scene, dt, exceptionHandler));
        }

        public void ProcessPendingUpdates()
        {
            if (pending.Count == 0)
                return;

            processing.Clear();
            processing.AddRange(pending);
            pending.Clear();

            for (var i = 0; i < processing.Count; i++)
            {
                (ISceneFacade scene, float dt, ISceneExceptionsHandler exceptionHandler) = processing[i];
                try
                {
                    scene.Tick(dt).GetAwaiter().GetResult();
                    scene.SceneStateProvider.TickNumber++;
                }
                catch (JavaScriptExecutionException e)
                {
                    exceptionHandler.OnJavaScriptException(e);
                }
            }

            processing.Clear();
        }
    }
}

#endif
