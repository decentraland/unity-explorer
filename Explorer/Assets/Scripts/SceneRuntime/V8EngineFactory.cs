using Microsoft.ClearScript.V8;
using UnityEngine;

public class V8EngineFactory
{
    public static V8ScriptEngine Create()
    {
        //V8Settings.GlobalFlags |= V8GlobalFlags.DisableBackgroundWork;
        
        var engine = new V8ScriptEngine(V8ScriptEngineFlags.EnableTaskPromiseConversion);
        //engine.DisableDynamicBinding = true;
        //engine.UseReflectionBindFallback = true;

        return engine;
    }
}