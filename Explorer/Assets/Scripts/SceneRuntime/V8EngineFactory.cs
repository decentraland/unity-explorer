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

        public JsMemorySizeInfo GetEnginesSumMemoryData()
        {
            var totalHeapSize = new JsMemorySizeInfo();

            foreach (V8ScriptEngine? engine in activeEngines.Values)
            {
                V8RuntimeHeapInfo? heapInfo = engine.GetRuntimeHeapInfo();
                totalHeapSize.UsedHeapSize += heapInfo.UsedHeapSize;
                totalHeapSize.TotalHeapSize += heapInfo.TotalHeapSize;
                totalHeapSize.HeapSizeLimit += heapInfo.HeapSizeLimit;
                totalHeapSize.TotalHeapSizeExecutable += heapInfo.TotalHeapSizeExecutable;
            }

            totalHeapSize.HeapSizeLimit /= (ulong)activeEngines.Count;

            totalHeapSize.UsedHeapSize = BytesFormatter.Convert(totalHeapSize.UsedHeapSize, BytesFormatter.DataSizeUnit.Byte, BytesFormatter.DataSizeUnit.Megabyte);
            totalHeapSize.TotalHeapSize = BytesFormatter.Convert(totalHeapSize.TotalHeapSize, BytesFormatter.DataSizeUnit.Byte, BytesFormatter.DataSizeUnit.Megabyte);

            return totalHeapSize;
        }

        public JsMemorySizeInfo GetEnginesMemoryDataForScene(SceneShortInfo sceneInfo) =>
            activeEngines.TryGetValue(sceneInfo, out V8ScriptEngine? engine)
                ? new JsMemorySizeInfo(engine.GetRuntimeHeapInfo())
                : new JsMemorySizeInfo();
    }

    public struct JsMemorySizeInfo
    {
        public float UsedHeapSize;
        public float HeapSizeLimit;
        public float TotalHeapSize;
        public float TotalHeapSizeExecutable;

        public JsMemorySizeInfo(V8RuntimeHeapInfo heapInfo)
        {
            UsedHeapSize = heapInfo.UsedHeapSize;
            TotalHeapSize = heapInfo.TotalHeapSize;
            HeapSizeLimit = heapInfo.HeapSizeLimit;
            TotalHeapSizeExecutable = heapInfo.TotalHeapSizeExecutable;
        }
    }
}
