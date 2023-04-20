using System;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;

public class V8EngineFactory
{
    public static V8ScriptEngine Create()
    {
        var engine = new V8ScriptEngine();

        return engine;
    }
}
