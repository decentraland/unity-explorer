using DCL.Diagnostics;
using Microsoft.ClearScript.V8;

namespace SceneRuntime
{
    public class V8EngineFactory
    {
        private readonly V8ActiveEngines activeEngines;

        public V8EngineFactory(V8ActiveEngines activeEngines)
        {
            this.activeEngines = activeEngines;
        }

        public V8ScriptEngine Create(SceneShortInfo sceneInfo)
        {
            var engine = new V8ScriptEngine();

            // IL2CPP does not support dynamic bindings!
            engine.DisableDynamicBinding = true;
            engine.UseReflectionBindFallback = true;
            engine.AllowReflection = true;

            activeEngines.TryAdd(sceneInfo, engine);

            return engine;
        }
    }
}
