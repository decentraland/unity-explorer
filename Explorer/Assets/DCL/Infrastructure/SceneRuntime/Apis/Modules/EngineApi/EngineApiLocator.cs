using System;
using System.Collections.Generic;
using DCL.Diagnostics;

namespace SceneRuntime.Apis.Modules.EngineApi
{
    /// <summary>
    ///     Простая регистрация EngineApi по сцене для доступа из ECS-систем.
    ///     Использует weak references, чтобы не удерживать объекты дольше их жизненного цикла.
    /// </summary>
    public static class EngineApiLocator
    {
        private static readonly object Sync = new ();
        private static readonly Dictionary<SceneShortInfo, WeakReference<IEngineApi>> Map = new ();

        public static void Register(SceneShortInfo sceneInfo, IEngineApi engineApi)
        {
            lock (Sync) { Map[sceneInfo] = new WeakReference<IEngineApi>(engineApi); }
        }

        public static void Unregister(SceneShortInfo sceneInfo, IEngineApi engineApi)
        {
            lock (Sync)
            {
                if (Map.TryGetValue(sceneInfo, out WeakReference<IEngineApi>? wr))
                {
                    if (!wr.TryGetTarget(out IEngineApi? current) || ReferenceEquals(current, engineApi))
                        Map.Remove(sceneInfo);
                }
            }
        }

        public static bool TryGet(SceneShortInfo sceneInfo, out IEngineApi engineApi)
        {
            lock (Sync)
            {
                engineApi = null;

                if (Map.TryGetValue(sceneInfo, out WeakReference<IEngineApi>? wr))
                {
                    if (wr.TryGetTarget(out IEngineApi? target))
                    {
                        engineApi = target;
                        return true;
                    }

                    // cleanup stale
                    Map.Remove(sceneInfo);
                }

                return false;
            }
        }
    }
}
