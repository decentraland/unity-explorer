using System.Collections;
using System.Collections.Generic;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using Microsoft.ClearScript.V8;
using UnityEngine;

public class SceneRuntime
{
    public readonly V8ScriptEngine Engine;
    
    public readonly SceneModuleLoader SceneModuleLoader;

    private readonly UnityOpsApi api; // TODO: This is only needed for the LifeCycle

    public SceneRuntime(string sourceCode)
    {
        SceneModuleLoader = new SceneModuleLoader(this);
        Engine = V8EngineFactory.Create();

        // Compile Scene Code
        var commonJsModule = Helpers.ModuleWrapperCommonJs(sourceCode);
        var sceneCode = Engine.Compile(commonJsModule);
        
        // Initialize init API
        api = new UnityOpsApi(this, sceneCode);

        // Load and Compile Js Modules
        SceneModuleLoader.LoadAndCompileJsModules();

        // Load the Scene Code
        Engine.Execute("globalUnity = require(\"~scene.js\")"); // TODO: We can avoid using `globalUnity` with a Evaluate, and calling the onStart/onUpdate on the evaluated code from C#
    }

    public void StartScene()
    {
        Engine.Execute("globalUnity.onStart()"); // this can be awaited
    }

    public void Update()
    {
        Engine.Execute($"globalUnity.onUpdate({Time.deltaTime})"); // this can be awaited
    }
}
