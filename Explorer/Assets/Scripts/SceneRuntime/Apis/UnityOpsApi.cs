using System.Text.RegularExpressions;
using JetBrains.Annotations;
using Microsoft.ClearScript.V8;
using UnityEngine;

public class UnityOpsApi
{
    private readonly SceneRuntime runtime;
    private readonly V8Script sceneScript;

    public UnityOpsApi(SceneRuntime runtime, V8Script sceneScript)
    {
        this.runtime = runtime;
        this.sceneScript = sceneScript;
        
        runtime.Engine.AddHostObject("UnityOpsApi", this);

        var sourceCode = Helpers.LoadJavaScriptSourceCode("Init.js");
        runtime.Engine.Execute(sourceCode);
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
        Debug.LogError(runtime.Engine.GetStackTrace());
    }

    [UsedImplicitly]
    public object LoadAndEvaluateCode(string moduleName)
    {
        // Load just Scene Code
        if (moduleName == "~scene.js")
            return runtime.Engine.Evaluate(sceneScript);

        var dirname = moduleName.Substring(0, 1);
        var filename = moduleName.Substring(1);
        
        // Load Unity-side implementation of the module
        runtime.SceneModuleLoader.LoadUnityImplementationModule(filename);
        
        // Load JavaScript wrapper in the Runtime
        var moduleScript = runtime.SceneModuleLoader.GetModuleScript(filename);
        return runtime.Engine.Evaluate(moduleScript);
    }
}