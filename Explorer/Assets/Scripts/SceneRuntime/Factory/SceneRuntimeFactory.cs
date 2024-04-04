using CommunicationData.URLHelpers;
using CrdtEcsBridge.Engine;
using CrdtEcsBridge.PoolsProviders;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision.CodeResolver;
using DCL.WebRequests;
using DCL.Diagnostics;
using SceneRunner.Scene.ExceptionsHandling;
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
            SwitchToThreadPool,
        }

        private readonly JsCodeResolver codeContentResolver;
        private readonly Dictionary<string, string> sourceCodeCache;

        public SceneRuntimeFactory(IWebRequestController webRequestController)
        {
            codeContentResolver = new JsCodeResolver(webRequestController);
            sourceCodeCache = new Dictionary<string, string>();
        }

        /// <summary>
        ///     Must be called on the main thread
        /// </summary>
        public async UniTask<SceneRuntimeImpl> CreateBySourceCodeAsync(
            string sourceCode,
            ISceneExceptionsHandler sceneExceptionsHandler,
            IInstancePoolsProvider instancePoolsProvider,
            SceneShortInfo sceneShortInfo,
            CancellationToken ct,
            InstantiationBehavior instantiationBehavior = InstantiationBehavior.StayOnMainThread)
        {
            AssertCalledOnTheMainThread();

            (string initSourceCode, Dictionary<string, string> moduleDictionary) = await UniTask.WhenAll(GetJsInitSourceCode(ct), GetJsModuleDictionaryAsync(ct));

            // On instantiation there is a bit of logic to execute by the scene runtime so we can benefit from the thread pool
            if (instantiationBehavior == InstantiationBehavior.SwitchToThreadPool)
                await UniTask.SwitchToThreadPool();

            // Provide basic Thread Pool synchronization context
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());

            return new SceneRuntimeImpl(sceneExceptionsHandler, WrapInModuleCommonJs(sourceCode), initSourceCode, moduleDictionary, instancePoolsProvider, sceneShortInfo);
        }

        /// <summary>
        ///     Must be called on the main thread
        /// </summary>
        public async UniTask<SceneRuntimeImpl> CreateByPathAsync(URLAddress path,
            ISceneExceptionsHandler sceneExceptionsHandler,
            IInstancePoolsProvider instancePoolsProvider,
            SceneShortInfo sceneShortInfo,
            CancellationToken ct,
            InstantiationBehavior instantiationBehavior = InstantiationBehavior.StayOnMainThread)
        {
            AssertCalledOnTheMainThread();

            string sourceCode = await LoadJavaScriptSourceCodeAsync(path, ct);
            return await CreateBySourceCodeAsync(sourceCode, sceneExceptionsHandler, instancePoolsProvider, sceneShortInfo, ct, instantiationBehavior);
        }

        private void AssertCalledOnTheMainThread()
        {
            if (!PlayerLoopHelper.IsMainThread)
                throw new ThreadStateException($"{nameof(CreateByPathAsync)} must be called on the main thread");
        }

        private UniTask<string> GetJsInitSourceCode(CancellationToken ct) =>
            LoadJavaScriptSourceCodeAsync(
                URLAddress.FromString($"file://{Application.streamingAssetsPath}/Js/Init.js"), ct);

        private async UniTask AddModuleAsync(string moduleName, IDictionary<string, string> moduleDictionary, CancellationToken ct) =>
            moduleDictionary.Add(moduleName, WrapInModuleCommonJs(await LoadJavaScriptSourceCodeAsync(
                URLAddress.FromString($"file://{Application.streamingAssetsPath}/Js/Modules/{moduleName}"), ct)));

        private async UniTask<Dictionary<string, string>> GetJsModuleDictionaryAsync(CancellationToken ct)
        {
            var moduleDictionary = new Dictionary<string, string>();

            await AddModuleAsync("EngineApi.js", moduleDictionary, ct);
            await AddModuleAsync("CommsApi.js", moduleDictionary, ct);
            await AddModuleAsync("EthereumController.js", moduleDictionary, ct);
            await AddModuleAsync("Players.js", moduleDictionary, ct);
            await AddModuleAsync("PortableExperiences.js", moduleDictionary, ct);
            await AddModuleAsync("RestrictedActions.js", moduleDictionary, ct);
            await AddModuleAsync("Runtime.js", moduleDictionary, ct);
            await AddModuleAsync("Scene.js", moduleDictionary, ct);
            await AddModuleAsync("SignedFetch.js", moduleDictionary, ct);
            await AddModuleAsync("Testing.js", moduleDictionary, ct);
            await AddModuleAsync("UserIdentity.js", moduleDictionary, ct);
            await AddModuleAsync("CommunicationsController.js", moduleDictionary, ct);
            await AddModuleAsync("EnvironmentApi.js", moduleDictionary, ct);
            await AddModuleAsync("UserActionModule.js", moduleDictionary, ct);

            return moduleDictionary;
        }

        private async UniTask<string> LoadJavaScriptSourceCodeAsync(URLAddress path, CancellationToken ct)
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
