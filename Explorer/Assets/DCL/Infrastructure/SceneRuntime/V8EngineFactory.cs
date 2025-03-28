using DCL.Diagnostics;
using Microsoft.ClearScript.V8;

namespace SceneRuntime
{
    public sealed class V8EngineFactory
    {
        public V8EngineFactory()
        {
            // TODO: Store information about which scene's engine to create with debugging enabled.
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
