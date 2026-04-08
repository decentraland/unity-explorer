using DCL.Diagnostics;
using Microsoft.ClearScript.V8;
using UnityEngine;

namespace SceneRuntime
{
    public sealed class V8EngineFactory : System.IDisposable
    {
        // All scene engines share one V8 isolate so their GC workers never run concurrently
        // against each other's EphemeronHashTable entries.  Each CreateScriptEngine call
        // produces an independent V8 context (heap + global object) inside the isolate, so
        // scenes remain fully isolated at the JS level.
        private readonly V8Runtime sharedRuntime;

        public V8EngineFactory()
        {
            sharedRuntime = new V8Runtime("DecentralandScenes");
            Application.quitting += Dispose;
        }

        public V8ScriptEngine Create(SceneShortInfo sceneInfo)
        {
            var engine = sharedRuntime.CreateScriptEngine(sceneInfo.ToString());

            // IL2CPP does not support dynamic bindings!
            engine.DisableDynamicBinding = true;
            engine.UseReflectionBindFallback = true;
            engine.AllowReflection = true;

            return engine;
        }

        public void Dispose()
        {
            Application.quitting -= Dispose;
            sharedRuntime.Dispose();
        }
    }
}
