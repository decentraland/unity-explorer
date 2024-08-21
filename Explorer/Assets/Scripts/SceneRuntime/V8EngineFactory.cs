using DCL.DebugUtilities;
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
            if (!activeEngines.ContainsKey(sceneInfo))
            {
                var engine = new V8ScriptEngine();

                // IL2CPP does not support dynamic bindings!
                engine.DisableDynamicBinding = true;
                engine.UseReflectionBindFallback = true;
                engine.AllowReflection = true;

                activeEngines[sceneInfo] = engine;
            }
            else
                ReportHub.LogError(ReportData.UNSPECIFIED, $"Trying to create V8 engine for scene that already have one running engine. Scene: {sceneInfo.ToString()}");

            return activeEngines[sceneInfo];
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

        public JsMemorySizeInfo GetEnginesSumMemoryData()
        {
            ulong usedHeapSize = 0;
            ulong totalHeapSize = 0;
            ulong heapSizeLimit = 0;
            ulong totalHeapSizeExecutable = 0;

            if (activeEngines.Values.Count > 0)
            {
                foreach (V8ScriptEngine? engine in activeEngines.Values)
                {
                    V8RuntimeHeapInfo? heapInfo = engine.GetRuntimeHeapInfo();

                    usedHeapSize += heapInfo.UsedHeapSize;
                    totalHeapSize += heapInfo.TotalHeapSize;
                    heapSizeLimit += heapInfo.HeapSizeLimit;
                    totalHeapSizeExecutable += heapInfo.TotalHeapSizeExecutable;
                }

                heapSizeLimit /= (ulong)activeEngines.Count;
            }

            return new JsMemorySizeInfo
            {
                UsedHeapSizeMB = usedHeapSize.ByteToMB(),
                TotalHeapSizeMB = totalHeapSize.ByteToMB(),
                HeapSizeLimitMB = heapSizeLimit.ByteToMB(),
                TotalHeapSizeExecutableMB = totalHeapSizeExecutable.ByteToMB(),
            };
        }

        public JsMemorySizeInfo GetEnginesMemoryDataForScene(SceneShortInfo sceneInfo) =>
            activeEngines.TryGetValue(sceneInfo, out V8ScriptEngine? engine)
                ? new JsMemorySizeInfo(engine.GetRuntimeHeapInfo())
                : new JsMemorySizeInfo();
    }

    public struct JsMemorySizeInfo
    {
        public float UsedHeapSizeMB;
        public float HeapSizeLimitMB;
        public float TotalHeapSizeMB;
        public float TotalHeapSizeExecutableMB;

        public JsMemorySizeInfo(V8RuntimeHeapInfo heapInfo)
        {
            UsedHeapSizeMB = heapInfo.UsedHeapSize.ByteToMB();
            TotalHeapSizeMB = heapInfo.TotalHeapSize.ByteToMB();
            HeapSizeLimitMB = heapInfo.HeapSizeLimit.ByteToMB();
            TotalHeapSizeExecutableMB = heapInfo.TotalHeapSizeExecutable.ByteToMB();
        }
    }
}
