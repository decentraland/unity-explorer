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

            if (!activeEngines.TryAdd(sceneInfo, engine))
            {
                ReportHub.LogWarning(ReportData.UNSPECIFIED, $"Trying to create same V8 engine for scene that already have one running engine. Replacing running engine with new one... Scene: {sceneInfo.ToString()}");
                activeEngines[sceneInfo].Dispose();
                activeEngines[sceneInfo] = engine;
            }

            activeEngines.TryAdd(sceneInfo, engine);

            return engine;
        }
    }
}
