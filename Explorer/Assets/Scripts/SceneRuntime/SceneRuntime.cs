using Cysharp.Threading.Tasks;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using Microsoft.ClearScript.V8;
using System;
using System.Collections.Generic;

public class SceneRuntime : IDisposable
{
    internal readonly V8ScriptEngine engine;

    private readonly SceneModuleLoader moduleLoader;
    private readonly UnityOpsApi unityOpsApi; // TODO: This is only needed for the LifeCycle
    private readonly ScriptObject sceneCode;
    private readonly ScriptObject updateFunc;
    private readonly ScriptObject startFunc;

    // ResetableSource is an optimization to reduce 11kb of memory allocation per Update (reduces 15kb to 4kb per update)
    private readonly UniTaskResolverResetable resetableSource;

    private IEngineApi engineApi;

    public SceneRuntime(string sourceCode, string jsInitCode, Dictionary<string, string> jsModules)
    {
        resetableSource = new UniTaskResolverResetable();
        moduleLoader = new SceneModuleLoader();
        engine = V8EngineFactory.Create();

        // Compile Scene Code
        var sceneScript = engine.Compile(sourceCode);

        // Initialize init API
        unityOpsApi = new UnityOpsApi(engine, moduleLoader, sceneScript);
        engine.AddHostObject("UnityOpsApi", unityOpsApi);
        engine.Execute(jsInitCode);

        // Setup unitask resolver
        engine.AddHostObject("__resetableSource", resetableSource);

        // Load and Compile Js Modules
        moduleLoader.LoadAndCompileJsModules(engine, jsModules);

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

    public void Dispose()
    {
        engineApi?.Dispose();
    }
}
