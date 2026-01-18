using CommunicationData.URLHelpers;
using CrdtEcsBridge.PoolsProviders;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization;
using ECS;
using SceneRuntime.Factory.WebSceneSource.Cache;
using SceneRuntime.Factory.JsSceneSourceCode;
using SceneRuntime.Factory.WebSceneSource;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;

#if UNITY_WEBGL

// ReSharper disable once RedundantUsingDirective
using SceneRuntime.WebClient;

#else
using SceneRuntime.V8;
#endif

namespace SceneRuntime.Factory
{
    public sealed class SceneRuntimeFactory
    {
        public enum InstantiationBehavior
        {
            STAY_ON_MAIN_THREAD,
            SWITCH_TO_THREAD_POOL,
        }

        private static readonly IReadOnlyCollection<string> JS_MODULE_NAMES = new JsModulesNameList().ToList();
        private readonly IRealmData? realmData;
        private readonly IJavaScriptEngineFactory engineFactory;

        private readonly IWebJsSources webJsSources;
        private readonly IJsSourcesCache jsSourcesCache;
        private readonly IJsSceneLocalSourceCode jsSceneLocalSourceCode = new IJsSceneLocalSourceCode.Default();

        public SceneRuntimeFactory(IRealmData? realmData, IJavaScriptEngineFactory engineFactory, IWebJsSources webJsSources)
        {
            this.realmData = realmData;
            this.engineFactory = engineFactory;
            jsSourcesCache = EnabledJsScenesFileCachingOrIgnore();
            this.webJsSources = webJsSources;
        }

        /// <summary>
        ///     How to use it
        ///     1. Ensure that the directory exists at the path DIR
        ///     2. Launch the Unity Editor and play scenes normally
        ///     3. Check the directory DIR for the cached files
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
        internal async UniTask<ISceneRuntime> CreateBySourceCodeAsync(
            string sourceCode,
            IInstancePoolsProvider instancePoolsProvider,
            SceneShortInfo sceneShortInfo,
            CancellationToken ct,
            InstantiationBehavior instantiationBehavior = InstantiationBehavior.STAY_ON_MAIN_THREAD)
        {
            await EnsureCalledOnMainThreadAsync();

            jsSourcesCache.Cache(
                $"{realmData?.RealmName} {sceneShortInfo.BaseParcel.x},{sceneShortInfo.BaseParcel.y} {sceneShortInfo.Name}.js",
                sourceCode
            );

            (string initCode, IReadOnlyDictionary<string, string> moduleDictionary) = await UniTask.WhenAll(GetJsInitSourceCodeAsync(ct), GetJsModuleDictionaryAsync(JS_MODULE_NAMES, ct));

            // On instantiation there is a bit of logic to execute by the scene runtime so we can benefit from the thread pool
            // Note: In WebGL, thread pool switching may not work properly, so we skip it
#if !UNITY_WEBGL
            if (instantiationBehavior == InstantiationBehavior.SWITCH_TO_THREAD_POOL)
                await UniTask.SwitchToThreadPool();
#endif

            // Provide basic Thread Pool synchronization context
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
            string wrappedSource = WrapInModuleCommonJs(jsSceneLocalSourceCode.CodeForScene(sceneShortInfo.BaseParcel) ?? sourceCode);

#if UNITY_WEBGL
            // WebClient JavaScript engine uses native interop which requires the main thread
            await UniTask.SwitchToMainThread();
            return new WebClientSceneRuntimeImpl(wrappedSource, initCode, moduleDictionary, sceneShortInfo, engineFactory);
#else
            return new V8SceneRuntimeImpl(wrappedSource, initCode, moduleDictionary, sceneShortInfo, engineFactory);
#endif
        }

        /// <summary>
        ///     Must be called on the main thread
        /// </summary>
        public async UniTask<ISceneRuntime> CreateByPathAsync(
            URLAddress path,
            IInstancePoolsProvider instancePoolsProvider,
            SceneShortInfo sceneShortInfo,
            CancellationToken ct,
            InstantiationBehavior instantiationBehavior = InstantiationBehavior.STAY_ON_MAIN_THREAD)
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

        private async UniTask<string> GetJsInitSourceCodeAsync(CancellationToken ct)
        {
            string streamingPath = Application.streamingAssetsPath;
            string url;

#if UNITY_WEBGL && !UNITY_EDITOR
            // In WebGL, Application.streamingAssetsPath already returns a full HTTP URL
            // e.g., "http://localhost:8800/StreamingAssets" or "http://localhost:8800/StreamingAssets/"
            // Normalize to ensure we have a trailing slash
            if (!streamingPath.EndsWith("/"))
                streamingPath += "/";
            url = $"{streamingPath}Js/Init.js";
#else
            // For Editor and other platforms, use file:// protocol
            url = $"file://{streamingPath}/Js/Init.js";
#endif

            return await webJsSources.SceneSourceCodeAsync(URLAddress.FromString(url), ct);
        }

        private async UniTask AddModuleAsync(string moduleName, IDictionary<string, string> moduleDictionary, CancellationToken ct)
        {
            string streamingPath = Application.streamingAssetsPath;
            string url;

#if UNITY_WEBGL && !UNITY_EDITOR
            // In WebGL, Application.streamingAssetsPath already returns a full HTTP URL
            // e.g., "http://localhost:8800/StreamingAssets" or "http://localhost:8800/StreamingAssets/"
            // Normalize to ensure we have a trailing slash
            if (!streamingPath.EndsWith("/"))
                streamingPath += "/";
            url = $"{streamingPath}Js/Modules/{moduleName}";
#else
            // For Editor and other platforms, use file:// protocol
            url = $"file://{streamingPath}/Js/Modules/{moduleName}";
#endif

            moduleDictionary.Add(moduleName, WrapInModuleCommonJs(await webJsSources.SceneSourceCodeAsync(
                URLAddress.FromString(url), ct)));
        }

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
