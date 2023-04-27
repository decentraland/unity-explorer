using AssetManagement.CodeResolver;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

public class SceneRuntimeFactory
{
    private readonly CodeContentResolver codeContentResolver;
    private readonly Dictionary<string, string> sourceCodeCache;

    public SceneRuntimeFactory()
    {
        codeContentResolver = new CodeContentResolver();
        sourceCodeCache = new Dictionary<string, string>();
    }

    public async UniTask<SceneRuntime> CreateBySourceCode(string sourceCode) =>
        new SceneRuntime(WrapInModuleCommonJs(sourceCode), await GetJsInitSourceCode(), await GetJsModuleDictionary());

    public async UniTask<SceneRuntime> CreateByPath(string path)
    {
        string sourceCode = await LoadJavaScriptSourceCode(path);
        return new SceneRuntime(WrapInModuleCommonJs(sourceCode), await GetJsInitSourceCode(), await GetJsModuleDictionary());
    }

    private UniTask<string> GetJsInitSourceCode() =>
        LoadJavaScriptSourceCode($"file://{Application.streamingAssetsPath}/Js/Init.js");

    private async UniTask<Dictionary<string, string>> GetJsModuleDictionary()
    {
        var moduleDictionary = new Dictionary<string, string>
        {
            {
                "EngineApi.js",
                WrapInModuleCommonJs(await LoadJavaScriptSourceCode($"file://{Application.streamingAssetsPath}/Js/Modules/EngineAPI.js"))
            },
        };
        return moduleDictionary;
    }

    private async UniTask<string> LoadJavaScriptSourceCode(string path)
    {
        if (sourceCodeCache.TryGetValue(path, out string value)) { return value; }

        string sourceCode = await codeContentResolver.GetCodeContent(path);
        sourceCodeCache.Add(path, sourceCode);
        return sourceCode;
    }

    // Wrapper https://nodejs.org/api/modules.html#the-module-wrapper
    // Wrap the source code in a CommonJS module wrapper
    internal string WrapInModuleCommonJs(string source)
    {
        // create a wrapper for the script
        source = Regex.Replace(source, @"^#!.*?\n", "");
        var head = "(function (exports, require, module, __filename, __dirname) { (function (exports, require, module, __filename, __dirname) {";
        var foot = "\n}).call(this, exports, require, module, __filename, __dirname); })";
        source = $"{head}{source}{foot}";
        return source;
    }
}
