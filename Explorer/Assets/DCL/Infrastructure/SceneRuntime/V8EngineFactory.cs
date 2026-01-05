using DCL.Diagnostics;
using Microsoft.ClearScript.V8;
using SceneRunner.Scene;

namespace SceneRuntime
{
    public sealed class V8EngineFactory : IJavaScriptEngineFactory
    {
        public V8EngineFactory()
        {
            // TODO: Store information about which scene's engine to create with debugging enabled.
        }

        public IJavaScriptEngine Create(SceneShortInfo sceneInfo)
        {
            var engine = new V8ScriptEngine(sceneInfo.ToString());

            // IL2CPP does not support dynamic bindings!
            engine.DisableDynamicBinding = true;
            engine.UseReflectionBindFallback = true;
            engine.AllowReflection = true;

            return new V8JavaScriptEngineAdapter(engine);
        }

        public V8ScriptEngine CreateV8Engine(SceneShortInfo sceneInfo)
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
