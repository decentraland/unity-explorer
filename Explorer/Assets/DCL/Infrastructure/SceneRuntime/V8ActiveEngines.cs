using DCL.DebugUtilities;
using DCL.Diagnostics;
using Microsoft.ClearScript.V8;
using System.Collections.Concurrent;

namespace SceneRuntime
{
    public class V8ActiveEngines : ConcurrentDictionary<SceneShortInfo, V8ScriptEngine>
    {
        public new bool TryAdd(SceneShortInfo sceneInfo, V8ScriptEngine engine)
        {
            if (base.TryAdd(sceneInfo, engine)) return true;

            ReportHub.LogWarning(ReportData.UNSPECIFIED, $"Trying to add V8 engine for scene that already has one running engine. Replacing running engine with new one... Scene: {sceneInfo}");

            if (TryGetValue(sceneInfo, out V8ScriptEngine? existingEngine))
                existingEngine.Dispose();

            return TryUpdate(sceneInfo, engine, existingEngine);
        }

        public bool TryRemove(SceneShortInfo scene, V8ScriptEngine engine)
        {
            if (base.TryRemove(scene, out V8ScriptEngine? removedEngine))
            {
                if (removedEngine != engine)
                    ReportHub.LogError(ReportData.UNSPECIFIED, $"V8Engine in the Dictionary for scene {scene.Name} was not the same as the one being disposed!");

                engine.Dispose();
                return true;
            }

            return false;
        }

        public new void Clear()
        {
            foreach (V8ScriptEngine? engine in Values)
                engine.Dispose();

            base.Clear();
        }

        public JsMemorySizeInfo GetEnginesSumMemoryData()
        {
            ulong usedHeapSize = 0;
            ulong totalHeapSize = 0;
            ulong heapSizeLimit = 0;
            ulong totalHeapSizeExecutable = 0;

            if (Count > 0)
            {
                foreach (V8ScriptEngine engine in Values)
                {
                    V8RuntimeHeapInfo heapInfo = engine.GetRuntimeHeapInfo();

                    usedHeapSize += heapInfo.UsedHeapSize;
                    totalHeapSize += heapInfo.TotalHeapSize;
                    heapSizeLimit += heapInfo.HeapSizeLimit;
                    totalHeapSizeExecutable += heapInfo.TotalHeapSizeExecutable;
                }

                heapSizeLimit /= (ulong)Count;
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
            TryGetValue(sceneInfo, out V8ScriptEngine? engine)
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
