using Cysharp.Threading.Tasks;
using System.Collections.Generic;

public class SceneRuntimeFactory
{
    private readonly Dictionary<string, string> sourceCodeCache;
    private readonly CodeContentProvider codeContentProvider;

    public SceneRuntimeFactory()
    {
        codeContentProvider = new CodeContentProvider();
        sourceCodeCache = new Dictionary<string, string>();
    }

    public async UniTask<SceneRuntime> CreateSceneRuntimeBySourceCode(string sourceCode)
    {
        string javascriptSourceCode = await LoadJavaScriptSourceCode("Js/Init.js");

        var moduleDictionary = new Dictionary<string, string>
            { { "EngineApi.js", await codeContentProvider.GetFileByStreamingAsset("Js/Modules/EngineAPI.js") } };

        return new SceneRuntime(sourceCode, javascriptSourceCode, moduleDictionary);
    }

    public async UniTask<SceneRuntime> CreateSceneRuntimeByPath(string path)
    {
        string sourceCode = await codeContentProvider.GetFileByStreamingAsset(path);
        string javascriptSourceCode = await LoadJavaScriptSourceCode("Js/Init.js");

        var moduleDictionary = new Dictionary<string, string>
            { { "EngineApi.js", await codeContentProvider.GetFileByStreamingAsset("Js/Modules/EngineAPI.js") } };

        return new SceneRuntime(sourceCode, javascriptSourceCode, moduleDictionary);
    }

    private async UniTask<string> LoadJavaScriptSourceCode(string fileName)
    {
        if (sourceCodeCache.TryGetValue(fileName, out string value)) { return value; }

        string sourceCode = await codeContentProvider.GetFileByStreamingAsset(fileName);
        sourceCodeCache.Add(fileName, sourceCode);
        return sourceCode;
    }
}
