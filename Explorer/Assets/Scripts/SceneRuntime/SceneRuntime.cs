using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using Microsoft.ClearScript.V8;
using UnityEngine;
using UnityEngine.Profiling;

public class SceneRuntime
{
    internal readonly V8ScriptEngine engine;

    private readonly SceneModuleLoader moduleLoader;

    private readonly UnityOpsApi unityOpsApi; // TODO: This is only needed for the LifeCycle

    private readonly ScriptObject sceneCode;

    public SceneRuntime(string sourceCode)
    {
        moduleLoader = new SceneModuleLoader();
        engine = V8EngineFactory.Create();

        // Compile Scene Code
        var commonJsModule = Helpers.ModuleWrapperCommonJs(sourceCode);
        var sceneScript = engine.Compile(commonJsModule);

        // Initialize init API
        unityOpsApi = new UnityOpsApi(engine, moduleLoader, sceneScript);
        engine.AddHostObject("UnityOpsApi", unityOpsApi);
        engine.Execute(Helpers.LoadJavaScriptSourceCode("Js/Init.js"));

        // Load and Compile Js Modules
        moduleLoader.LoadAndCompileJsModules(engine);

        // Load the Scene Code
        sceneCode = engine.Evaluate(@"require('~scene.js')") as ScriptObject;
    }

    public void RegisterEngineApi(IEngineApi api)
    {
        engine.AddHostObject("UnityEngineApi", new EngineApiWrapper(api));
    }

    public UniTask StartScene()
    {
        return sceneCode.InvokeMethod("onStart").ToTask().AsUniTask();
    }

    public UniTask UpdateScene(float dt)
    {
        // TODO: Improve performance .ToTask() (overhead alloc 11kb each call)
        return sceneCode.InvokeMethod("onUpdate", dt).ToTask().AsUniTask();
    }
}
