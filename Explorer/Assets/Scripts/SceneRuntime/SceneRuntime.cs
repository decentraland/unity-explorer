using Cysharp.Threading.Tasks;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using Microsoft.ClearScript.V8;

public class SceneRuntime
{
    internal readonly V8ScriptEngine engine;
    
    private readonly SceneModuleLoader moduleLoader;

    private readonly UnityOpsApi api; // TODO: This is only needed for the LifeCycle

    private readonly ScriptObject sceneScriptObject;

    public SceneRuntime(string sourceCode)
    {
        moduleLoader = new SceneModuleLoader();
        engine = V8EngineFactory.Create();

        // Compile Scene Code
        var commonJsModule = Helpers.ModuleWrapperCommonJs(sourceCode);
        var sceneScript = engine.Compile(commonJsModule);
        
        // Initialize init API
        api = new UnityOpsApi(engine, moduleLoader, sceneScript);
        engine.AddHostObject("UnityOpsApi", api);
        engine.Execute(Helpers.LoadJavaScriptSourceCode("Js/Init.js"));

        // Load and Compile Js Modules
        moduleLoader.LoadAndCompileJsModules(engine);

        // Load the Scene Code
        sceneScriptObject = engine.Evaluate("require(\"~scene.js\")") as ScriptObject; // TODO: We can avoid using `globalUnity` with a Evaluate, and calling the onStart/onUpdate on the evaluated code from C#
    }

    public void RegisterEngineApi(IEngineApi api)
    {
        engine.AddHostObject("UnityEngineApi", new EngineApiWrapper(api));
    }

    public UniTask StartScene()
    {
        return sceneScriptObject.InvokeMethod("onStart").ToTask().AsUniTask();
    }

    public UniTask Update(float dt)
    {
        return sceneScriptObject.InvokeMethod("onUpdate", dt).ToTask().AsUniTask();
    }
}
