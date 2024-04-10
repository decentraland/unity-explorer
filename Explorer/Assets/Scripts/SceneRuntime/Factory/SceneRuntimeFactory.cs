using CommunicationData.URLHelpers;
using CrdtEcsBridge.PoolsProviders;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision.CodeResolver;
using DCL.WebRequests;
using DCL.Diagnostics;
using SceneRuntime.Factory.JsSceneSourceCode;
using System.Collections.Generic;
using System.Linq;
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
            SwitchToThreadPool,
        }

        private readonly JsCodeResolver codeContentResolver;
        private readonly Dictionary<string, string> sourceCodeCache;

        private static readonly IReadOnlyCollection<string> JS_MODULE_NAMES = new JsModulesNameList().ToList();
        private readonly IJsSceneSourceCode jsSceneSourceCode = new IJsSceneSourceCode.Default();

        public SceneRuntimeFactory(IWebRequestController webRequestController)
        {
            codeContentResolver = new JsCodeResolver(webRequestController);
            sourceCodeCache = new Dictionary<string, string>();
        }

        /// <summary>
        ///     Must be called on the main thread
        /// </summary>
        internal async UniTask<SceneRuntimeImpl> CreateBySourceCodeAsync(
            string sourceCode,
            IInstancePoolsProvider instancePoolsProvider,
            SceneShortInfo sceneShortInfo,
            CancellationToken ct,
            InstantiationBehavior instantiationBehavior = InstantiationBehavior.StayOnMainThread)
        {
            AssertCalledOnTheMainThread();

            (var pair, IReadOnlyDictionary<string, string> moduleDictionary) = await UniTask.WhenAll(GetJsInitSourceCodeAsync(ct), GetJsModuleDictionaryAsync(JS_MODULE_NAMES, ct));

            // On instantiation there is a bit of logic to execute by the scene runtime so we can benefit from the thread pool
            if (instantiationBehavior == InstantiationBehavior.SwitchToThreadPool)
                await UniTask.SwitchToThreadPool();

            // Provide basic Thread Pool synchronization context
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
            string wrappedSource = jsSceneSourceCode.CodeForScene(sceneShortInfo.BaseParcel) ?? WrapInModuleCommonJs(sourceCode);
            return new SceneRuntimeImpl(wrappedSource, pair, moduleDictionary, instancePoolsProvider, sceneShortInfo);
        }

        /// <summary>
        ///     Must be called on the main thread
        /// </summary>
        public async UniTask<SceneRuntimeImpl> CreateByPathAsync(
            URLAddress path,
            IInstancePoolsProvider instancePoolsProvider,
            SceneShortInfo sceneShortInfo,
            CancellationToken ct,
            InstantiationBehavior instantiationBehavior = InstantiationBehavior.StayOnMainThread)
        {
            AssertCalledOnTheMainThread();

            string sourceCode = await LoadJavaScriptSourceCodeAsync(path, ct);
            return await CreateBySourceCodeAsync(sourceCode, instancePoolsProvider, sceneShortInfo, ct, instantiationBehavior);
        }

        private void AssertCalledOnTheMainThread()
        {
            if (!PlayerLoopHelper.IsMainThread)
                throw new ThreadStateException($"{nameof(CreateByPathAsync)} must be called on the main thread");
        }

        private async UniTask<(string validateCode, string initCode)> GetJsInitSourceCodeAsync(CancellationToken ct)
        {
            string validateCode = await LoadJavaScriptSourceCodeAsync(
                URLAddress.FromString($"file://{Application.streamingAssetsPath}/Js/ValidatesMin.js"),
                ct
            );

            string initCode = await LoadJavaScriptSourceCodeAsync(
                URLAddress.FromString($"file://{Application.streamingAssetsPath}/Js/Init.js"),
                ct
            );

            return (validateCode, initCode);
        }

        private async UniTask AddModuleAsync(string moduleName, IDictionary<string, string> moduleDictionary, CancellationToken ct) =>
            moduleDictionary.Add(moduleName, WrapInModuleCommonJs(await LoadJavaScriptSourceCodeAsync(
                URLAddress.FromString($"file://{Application.streamingAssetsPath}/Js/Modules/{moduleName}"), ct)));

        private async UniTask<IReadOnlyDictionary<string, string>> GetJsModuleDictionaryAsync(IReadOnlyCollection<string> names, CancellationToken ct)
        {
            var moduleDictionary = new Dictionary<string, string>();
            foreach (string name in names) await AddModuleAsync(name, moduleDictionary, ct);
            return moduleDictionary;
        }

        private async UniTask<string> LoadJavaScriptSourceCodeAsync(URLAddress path, CancellationToken ct)
        {
            if (sourceCodeCache.TryGetValue(path, out string value)) return value!;

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
