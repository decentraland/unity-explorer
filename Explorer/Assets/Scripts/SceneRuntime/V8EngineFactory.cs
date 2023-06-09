using Microsoft.ClearScript.V8;

namespace SceneRuntime
{
    public class V8EngineFactory
    {
        public static V8ScriptEngine Create()
        {
            var engine = new V8ScriptEngine();

            // IL2CPP does not support dynamic bindings!
            engine.DisableDynamicBinding = true;
            engine.UseReflectionBindFallback = true;
            engine.AllowReflection = true;

            return engine;
        }
    }
}
