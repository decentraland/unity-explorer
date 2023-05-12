using Microsoft.ClearScript.V8;

namespace SceneRuntime
{
    public class V8EngineFactory
    {
        public static V8ScriptEngine Create()
        {
            var engine = new V8ScriptEngine();

            return engine;
        }
    }
}
