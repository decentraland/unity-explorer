using DCL.Diagnostics;
using Microsoft.ClearScript.V8;

namespace SceneRuntime
{
    public sealed class V8EngineFactory
    {
        public V8EngineFactory()
        {
            // TODO: Store information about which scene's engine to create with debugging enabled.

            // Disables V8 background worker threads. Without this, ClearScript routes V8's GC
            // background workers through the IL2CPP managed thread pool, where they run concurrently
            // with the main V8 thread's write barriers. This causes unsynchronised access to
            // EphemeronRememberedSet (V8's internal bookkeeping for WeakMap/WeakSet GC) and
            // corrupts its internal unordered_map, crashing in ClearWeakCollections().
            V8Settings.GlobalFlags |= V8GlobalFlags.DisableBackgroundWork;
        }

        public V8ScriptEngine Create(SceneShortInfo sceneInfo)
        {
            var engine = new V8ScriptEngine(sceneInfo.ToString());

            // IL2CPP does not support dynamic bindings!
            engine.DisableDynamicBinding = true;
            engine.UseReflectionBindFallback = true;
            engine.AllowReflection = true;

            return engine;
        }
    }
}
