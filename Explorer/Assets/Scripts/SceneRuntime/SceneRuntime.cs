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

    private readonly ScriptObject updateFunc;
    private readonly ScriptObject startFunc;

    // ResetableSource is an optimization to reduce 11kb of memory allocation per Update (reduces 15kb to 4kb per update)
    private readonly UniTaskResolverResetable resetableSource;

    public SceneRuntime(string sourceCode)
    {
        resetableSource  = new UniTaskResolverResetable();
        moduleLoader = new SceneModuleLoader();
        engine = V8EngineFactory.Create();

        // Compile Scene Code
        var commonJsModule = Helpers.ModuleWrapperCommonJs(sourceCode);
        var sceneScript = engine.Compile(commonJsModule);

        // Initialize init API
        unityOpsApi = new UnityOpsApi(engine, moduleLoader, sceneScript);
        engine.AddHostObject("UnityOpsApi", unityOpsApi);
        engine.Execute(Helpers.LoadJavaScriptSourceCode("Js/Init.js"));

        // Setup unitask resolver
        engine.AddHostObject("__resetableSource", resetableSource);

        // Load and Compile Js Modules
        moduleLoader.LoadAndCompileJsModules(engine);

        engine.Execute(@"
            const __internalScene = require('~scene.js')
            const __internalOnUpdate = async function (dt) {
                try {
                    await __internalScene.onUpdate(dt)
                    __resetableSource.Completed()
                } catch(e) {
                    __resetableSource.Reject(e)
                }
            }
        ");

        updateFunc = (ScriptObject)engine.Evaluate("__internalOnUpdate");
        startFunc = (ScriptObject)engine.Evaluate("__internalScene.onStart");
    }

    public void RegisterEngineApi(IEngineApi api)
    {
        engine.AddHostObject("UnityEngineApi", new EngineApiWrapper(api));
    }

    public UniTask StartScene()
    {
        return startFunc.InvokeAsFunction().ToTask().AsUniTask();
    }

    public UniTask UpdateScene(float dt)
    {
        resetableSource.Reset();
        updateFunc.InvokeAsFunction(dt);
        return resetableSource.Task;
    }
}
