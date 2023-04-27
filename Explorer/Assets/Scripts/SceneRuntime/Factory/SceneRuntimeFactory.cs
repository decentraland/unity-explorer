using AssetManagement.CodeResolver;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class SceneRuntimeFactory
{
    private readonly CodeContentResolver codeContentResolver;
    private readonly Dictionary<string, string> sourceCodeCache;

    public SceneRuntimeFactory()
    {
        this.codeContentResolver = new CodeContentResolver();
        sourceCodeCache = new Dictionary<string, string>();
    }

    public async UniTask<SceneRuntime> CreateBySourceCode(string sourceCode)
    {
        string jsInitSourceCode = await LoadJavaScriptSourceCode("Js/Init.js");
        var moduleDictionary = new Dictionary<string, string>
        {
            {
                "EngineApi.js",
                WrapInModuleCommonJs(await LoadJavaScriptSourceCode("Js/Modules/EngineAPI.js"))
            },
        };

        return new SceneRuntime(WrapInModuleCommonJs(sourceCode), jsInitSourceCode, moduleDictionary);
    }

    public async UniTask<SceneRuntime> CreateByPath(string path)
    {
        string sourceCode = await LoadJavaScriptSourceCode(path);
        string javascriptSourceCode = await LoadJavaScriptSourceCode("Js/Init.js");
        var moduleDictionary = new Dictionary<string, string>
        {
            {
                "EngineApi.js",
                WrapInModuleCommonJs(await LoadJavaScriptSourceCode("Js/Modules/EngineAPI.js"))
            },
        };

        return new SceneRuntime(WrapInModuleCommonJs(sourceCode), javascriptSourceCode, moduleDictionary);
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
