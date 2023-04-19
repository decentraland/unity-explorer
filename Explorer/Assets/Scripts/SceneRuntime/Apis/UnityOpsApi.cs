using System.Text.RegularExpressions;
using JetBrains.Annotations;
using Microsoft.ClearScript.V8;
using UnityEngine;

public class UnityOpsApi
{
    private readonly V8ScriptEngine engine;
    private readonly SceneModuleLoader moduleLoader;
    private readonly V8Script sceneScript;

    public UnityOpsApi(V8ScriptEngine engine, SceneModuleLoader moduleLoader, V8Script sceneScript)
    {
        this.engine = engine;
        this.moduleLoader = moduleLoader;
        this.sceneScript = sceneScript;
    }

    [UsedImplicitly]
    public void Log(object message)
    {
        Debug.Log(message);
    }

    [UsedImplicitly]
    public void Warning(object message)
    {
        Debug.LogWarning(message);
    }
    
    [UsedImplicitly]
    public void Error(object message)
    {
        Debug.LogError(message);
        Debug.LogError(engine.GetStackTrace());
    }

    [UsedImplicitly]
    public object LoadAndEvaluateCode(string moduleName) // "~system/EngineApi"
    {
        // Load just Scene Code
        if (moduleName == "~scene.js")
            return engine.Evaluate(sceneScript);

        var dirname = moduleName.Substring(0, 1);
        var filename = moduleName.Substring(1);

        // Load JavaScript wrapper in the Runtime
        var moduleScript = moduleLoader.GetModuleScript(filename);
        return engine.Evaluate(moduleScript);
    }
}