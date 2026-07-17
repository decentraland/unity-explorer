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

            // Scene JavaScript is untrusted (any parcel can serve arbitrary code). AllowReflection
            // would let it call GetType()/typeOf()/TargetSite on the injected host objects and walk
            // from there to any loaded .NET type - a sandbox escape. It must stay disabled; it does
            // not affect normal host-member binding, which runs on UseReflectionBindFallback above.
            engine.AllowReflection = false;

            return engine;
        }
    }
}
