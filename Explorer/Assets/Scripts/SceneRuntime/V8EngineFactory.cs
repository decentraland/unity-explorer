using DCL.Diagnostics;
using Microsoft.ClearScript.V8;
using System.Collections.Concurrent;

namespace SceneRuntime
{
    public class V8EngineFactory
    {
        private readonly ConcurrentDictionary<SceneShortInfo, V8ScriptEngine> activeEngines = new ();
        public int ActiveEnginesCount => activeEngines.Count;

        public V8ScriptEngine Create(SceneShortInfo sceneInfo)
        {
            var engine = new V8ScriptEngine();

            // IL2CPP does not support dynamic bindings!
            engine.DisableDynamicBinding = true;
            engine.UseReflectionBindFallback = true;
            engine.AllowReflection = true;

            if (!activeEngines.TryAdd(sceneInfo, engine))
            {
                activeEngines[sceneInfo].Dispose();
                activeEngines[sceneInfo] = engine;
            }

            return engine;
        }

        public void DisposeEngine(SceneShortInfo scene, V8ScriptEngine engine)
        {
            activeEngines.TryRemove(scene, out V8ScriptEngine? removedEngine);

            if (removedEngine != engine)
                ReportHub.LogError(ReportData.UNSPECIFIED, $"V8Engine in the Dictionary for scene {scene.Name} was not the same as the one being disposed!");

            engine.Dispose();
        }

        public void DisposeAll()
        {
            foreach (V8ScriptEngine? engine in activeEngines.Values)
                engine.Dispose();
        }

        public ulong GetTotalJsHeapSizeInMB()
        {
            ulong totalHeapSize = 0;

            foreach (V8ScriptEngine? engine in activeEngines.Values)
                totalHeapSize += engine.GetRuntimeHeapInfo().UsedHeapSize;

            return  totalHeapSize;
        }

        public long GetJsHeapSizeBySceneInfo(SceneShortInfo sceneInfo)
        {
            if (activeEngines.TryGetValue(sceneInfo, out V8ScriptEngine? engine))
                return (long)engine.GetRuntimeHeapInfo().UsedHeapSize;

            return -1;
        }
    }
}
