using CommunicationData.URLHelpers;
using CrdtEcsBridge.PoolsProviders;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization;
using ECS;
using SceneRuntime.Factory.JsSceneSourceCode;
using SceneRuntime.Factory.WebSceneSource;
using SceneRuntime.Factory.WebSceneSource.Cache;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;

namespace SceneRuntime.Factory
{
    public sealed class SceneRuntimeFactory
    {
        private readonly IRealmData realmData;
        private readonly V8EngineFactory engineFactory;

        public enum InstantiationBehavior
        {
            StayOnMainThread,
            SwitchToThreadPool,
        }

        private readonly IWebJsSources webJsSources;
        private readonly IJsSourcesCache jsSourcesCache;

        private static readonly IReadOnlyCollection<string> JS_MODULE_NAMES = new JsModulesNameList().ToList();
        private readonly IJsSceneLocalSourceCode jsSceneLocalSourceCode = new IJsSceneLocalSourceCode.Default();

        public SceneRuntimeFactory(IRealmData realmData, V8EngineFactory engineFactory,
            IWebJsSources webJsSources)
        {
            this.realmData = realmData;
            this.engineFactory = engineFactory;
            jsSourcesCache = EnabledJsScenesFileCachingOrIgnore();
            this.webJsSources = webJsSources;
        }

        /// <summary>
        /// How to use it
        /// 1. Ensure that the directory exists at the path DIR
        /// 2. Launch the Unity Editor and play scenes normally
        /// 3. Check the directory DIR for the cached files
        ///     3.1 Some of them can be minified, use https://www.unminify2.com/ to explore them comfortably
        /// </summary>
        /// <returns>Cache for scenes</returns>
        private static IJsSourcesCache EnabledJsScenesFileCachingOrIgnore()
        {
            IJsSourcesCache cache = new IJsSourcesCache.Null();

#if UNITY_EDITOR
            const string DIR = "Assets/DCL/ScenesDebug/ScenesConsistency/JsCodes";

            if (Directory.Exists(DIR))
                cache = new FileJsSourcesCache(DIR);
            else
                ReportHub.Log(ReportCategory.SCENE_FACTORY, $"You can use {DIR} to persist loaded scenes");
#endif
            return cache;
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
            await EnsureCalledOnMainThreadAsync();

            jsSourcesCache.Cache(
                $"{realmData.RealmName} {sceneShortInfo.BaseParcel.x},{sceneShortInfo.BaseParcel.y} {sceneShortInfo.Name}.js",
                sourceCode
            );

            (var pair, IReadOnlyDictionary<string, string> moduleDictionary) = await UniTask.WhenAll(GetJsInitSourceCodeAsync(ct), GetJsModuleDictionaryAsync(JS_MODULE_NAMES, ct));

            // On instantiation there is a bit of logic to execute by the scene runtime so we can benefit from the thread pool
            if (instantiationBehavior == InstantiationBehavior.SwitchToThreadPool)
                await UniTask.SwitchToThreadPool();

            // Provide basic Thread Pool synchronization context
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
            string wrappedSource = WrapInModuleCommonJs(jsSceneLocalSourceCode.CodeForScene(sceneShortInfo.BaseParcel) ?? sourceCode);
            
            return new SceneRuntimeImpl(wrappedSource, pair, moduleDictionary, sceneShortInfo,
                engineFactory);
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
            await EnsureCalledOnMainThreadAsync();
            string sourceCode = await webJsSources.SceneSourceCodeAsync(path, ct);
            return await CreateBySourceCodeAsync(sourceCode, instancePoolsProvider, sceneShortInfo, ct, instantiationBehavior);
        }

        private static async UniTask EnsureCalledOnMainThreadAsync()
        {
            if (!PlayerLoopHelper.IsMainThread)
            {
                ReportHub.Log(ReportCategory.SCENE_FACTORY, $"{nameof(CreateByPathAsync)} must be called on the main thread");
                await UniTask.SwitchToMainThread();
            }
        }

        private async UniTask<(string validateCode, string initCode)> GetJsInitSourceCodeAsync(CancellationToken ct)
        {
            string validateCode = await webJsSources.SceneSourceCodeAsync(
                URLAddress.FromString($"file://{Application.streamingAssetsPath}/Js/ValidatesMin.js"),
                ct
            );

            string initCode = await webJsSources.SceneSourceCodeAsync(
                URLAddress.FromString($"file://{Application.streamingAssetsPath}/Js/Init.js"),
                ct
            );

            return (validateCode, initCode);
        }

        private async UniTask AddModuleAsync(string moduleName, IDictionary<string, string> moduleDictionary, CancellationToken ct) =>
            moduleDictionary.Add(moduleName, WrapInModuleCommonJs(await webJsSources.SceneSourceCodeAsync(
                URLAddress.FromString($"file://{Application.streamingAssetsPath}/Js/Modules/{moduleName}"), ct)));

        private async UniTask<IReadOnlyDictionary<string, string>> GetJsModuleDictionaryAsync(IReadOnlyCollection<string> names, CancellationToken ct)
        {
            var moduleDictionary = new Dictionary<string, string>();
            foreach (string name in names) await AddModuleAsync(name, moduleDictionary, ct);
            return moduleDictionary;
        }

        // Wrapper https://nodejs.org/api/modules.html#the-module-wrapper
        // Wrap the source code in a CommonJS module wrapper
        internal string WrapInModuleCommonJs(string source)
        {
            const string HEAD = "(function (exports, require, module, __filename, __dirname) { (function (exports, require, module, __filename, __dirname) {";
            const string FOOT = "\n}).call(this, exports, require, module, __filename, __dirname); })";

            if (source.StartsWith(HEAD))
                return source;

            // create a wrapper for the script
            source = Regex.Replace(source, @"^#!.*?\n", "");
            source = $"{HEAD}{source}{FOOT}";
            return source;
        }
    }
}
