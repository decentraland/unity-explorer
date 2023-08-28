using CrdtEcsBridge.Engine;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision.CodeResolver;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;

namespace SceneRuntime.Factory
{
    public class SceneRuntimeFactory
    {
        public enum InstantiationBehavior
        {
            StayOnMainThread,
            SwitchToThreadPool
        }

        private readonly JsCodeResolver codeContentResolver;
        private readonly Dictionary<string, string> sourceCodeCache;

        public SceneRuntimeFactory()
        {
            codeContentResolver = new JsCodeResolver();
            sourceCodeCache = new Dictionary<string, string>();
        }

        /// <summary>
        /// Must be called on the main thread
        /// </summary>
        public async UniTask<SceneRuntimeImpl> CreateBySourceCode(string sourceCode,
            IInstancePoolsProvider instancePoolsProvider,
            CancellationToken ct,
            InstantiationBehavior instantiationBehavior = InstantiationBehavior.StayOnMainThread)
        {
            AssertCalledOnTheMainThread();

            var (initSourceCode, moduleDictionary) = await UniTask.WhenAll(GetJsInitSourceCode(ct), GetJsModuleDictionary(ct));

            // On instantiation there is a bit of logic to execute by the scene runtime so we can benefit from the thread pool
            if (instantiationBehavior == InstantiationBehavior.SwitchToThreadPool)
                await UniTask.SwitchToThreadPool();

            // Provide basic Thread Pool synchronization context
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());

            return new SceneRuntimeImpl(WrapInModuleCommonJs(sourceCode), initSourceCode, moduleDictionary, instancePoolsProvider);
        }

        /// <summary>
        /// Must be called on the main thread
        /// </summary>
        public async UniTask<SceneRuntimeImpl> CreateByPath(string path,
            IInstancePoolsProvider instancePoolsProvider,
            CancellationToken ct,
            InstantiationBehavior instantiationBehavior = InstantiationBehavior.StayOnMainThread)
        {
            AssertCalledOnTheMainThread();

            string sourceCode = await LoadJavaScriptSourceCode(path, ct);
            return await CreateBySourceCode(sourceCode, instancePoolsProvider, ct, instantiationBehavior);
        }

        private void AssertCalledOnTheMainThread()
        {
            if (!PlayerLoopHelper.IsMainThread)
                throw new ThreadStateException($"{nameof(CreateByPath)} must be called on the main thread");
        }

        private UniTask<string> GetJsInitSourceCode(CancellationToken ct) =>
            LoadJavaScriptSourceCode($"file://{Application.streamingAssetsPath}/Js/Init.js", ct);

        public UniTask<string> GetJsSdk7AdaptionLayer(CancellationToken ct) =>
            LoadJavaScriptSourceCode($"file://{Application.streamingAssetsPath}/Js/Sdk7AdapterLayer.js", ct);

        private async UniTask AddModule(string moduleName, IDictionary<string, string> moduleDictionary, CancellationToken ct) =>
            moduleDictionary.Add(moduleName, WrapInModuleCommonJs(await LoadJavaScriptSourceCode($"file://{Application.streamingAssetsPath}/Js/Modules/{moduleName}", ct)));

        private async UniTask<Dictionary<string, string>> GetJsModuleDictionary(CancellationToken ct)
        {
            var moduleDictionary = new Dictionary<string, string>();

            await AddModule("EngineApi.js", moduleDictionary, ct);
            await AddModule("EthereumController.js", moduleDictionary, ct);
            await AddModule("Players.js", moduleDictionary, ct);
            await AddModule("PortableExperiences.js", moduleDictionary, ct);
            await AddModule("RestrictedActions.js", moduleDictionary, ct);
            await AddModule("Runtime.js", moduleDictionary, ct);
            await AddModule("Scene.js", moduleDictionary, ct);
            await AddModule("SignedFetch.js", moduleDictionary, ct);
            await AddModule("Testing.js", moduleDictionary, ct);
            await AddModule("UserIdentity.js", moduleDictionary, ct);

            return moduleDictionary;
        }

        private async UniTask<string> LoadJavaScriptSourceCode(string path, CancellationToken ct)
        {
            if (sourceCodeCache.TryGetValue(path, out string value)) return value;

            string sourceCode = await codeContentResolver.GetCodeContent(path, ct);

            // Replace instead of adding to fix the issue with possible loading of the same scene several times in parallel
            // (it should be a real scenario)
            sourceCodeCache[path] = sourceCode;
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
}
