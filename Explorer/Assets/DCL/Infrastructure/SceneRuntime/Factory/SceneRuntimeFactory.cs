using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization;
using DCL.Utility.Types;
using ECS;
using Microsoft.ClearScript.V8;
using SceneRuntime.Apis;
using SceneRuntime.Factory.JsSceneSourceCode;
using SceneRuntime.Factory.JsSource;
using SceneRuntime.Factory.WebSceneSource;
using SceneRuntime.Factory.WebSceneSource.Cache;
using SceneRuntime.ModuleHub;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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

        private static readonly byte[] COMMONJS_HEADER_UTF8 = Encoding.UTF8.GetBytes(
            "(function (exports, require, module, __filename, __dirname) { (function (exports, require, module, __filename, __dirname) {");

        private static readonly byte[] COMMONJS_FOOTER_UTF8 = Encoding.UTF8.GetBytes(
            "\n}).call(this, exports, require, module, __filename, __dirname); })");

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
            DownloadedOrCachedData sourceCode,
            SceneShortInfo sceneShortInfo,
            CancellationToken ct,
            InstantiationBehavior instantiationBehavior = InstantiationBehavior.StayOnMainThread)
        {
            await EnsureCalledOnMainThreadAsync();

            jsSourcesCache.Cache(
                $"{realmData.RealmName} {sceneShortInfo.BaseParcel.x},{sceneShortInfo.BaseParcel.y} {sceneShortInfo.Name}.js",
                sourceCode
            );

            // On instantiation there is a bit of logic to execute by the scene runtime so we can benefit from the thread pool
            if (instantiationBehavior == InstantiationBehavior.SwitchToThreadPool)
                await UniTask.SwitchToThreadPool();

            // Provide basic Thread Pool synchronization context
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());

            V8ScriptEngine engine = engineFactory.Create(sceneShortInfo);
            var moduleHub = new SceneModuleHub(engine);

            const int SIZE_OF_THE_LARGEST_MODULE_PLUS_EXTRA = 29000;
            byte[] buffer = new byte[SIZE_OF_THE_LARGEST_MODULE_PLUS_EXTRA];
            COMMONJS_HEADER_UTF8.CopyTo(buffer, 0);

            foreach (string moduleName in JS_MODULE_NAMES)
            {
                Result<int> moduleCodeLengthResult = await LoadScriptAsync(
                    Path.Combine(Application.streamingAssetsPath, "Js/Modules", moduleName), buffer,
                    COMMONJS_HEADER_UTF8.Length);

                if (!moduleCodeLengthResult.Success)
                    throw new Exception(moduleCodeLengthResult.ErrorMessage);

                int moduleCodeLength = moduleCodeLengthResult.Value;

                if (buffer.AsSpan(COMMONJS_HEADER_UTF8.Length, COMMONJS_HEADER_UTF8.Length)
                          .SequenceEqual(COMMONJS_HEADER_UTF8))
                {
                    moduleHub.LoadAndCompileJsModule(moduleName,
                        buffer.AsSpan(COMMONJS_HEADER_UTF8.Length, moduleCodeLength));
                }
                else
                {
                    moduleCodeLength += COMMONJS_HEADER_UTF8.Length;
                    COMMONJS_FOOTER_UTF8.CopyTo(buffer, moduleCodeLength);
                    moduleCodeLength += COMMONJS_FOOTER_UTF8.Length;

                    moduleHub.LoadAndCompileJsModule(moduleName, buffer.AsSpan(0, moduleCodeLength));
                }
            }

            V8Script sceneScript;

            if (sourceCode.Length >= COMMONJS_HEADER_UTF8.Length
                && sourceCode.AsReadOnlySpan()
                             .Slice(0, COMMONJS_HEADER_UTF8.Length)
                             .SequenceEqual(COMMONJS_HEADER_UTF8))
                sceneScript = engine.CompileScriptFromUtf8(sourceCode);
            else
            {
                ReportHub.LogWarning(ReportCategory.SCENE_FACTORY,
                    $"The code of the scene \"{sceneShortInfo.Name}\" at parcel {sceneShortInfo.BaseParcel} does not include the CommonJS module wrapper. This is suboptimal.");

                int wrappedCodeLength = COMMONJS_HEADER_UTF8.Length + sourceCode.Length
                                                                   + COMMONJS_FOOTER_UTF8.Length;

                if (buffer.Length < wrappedCodeLength)
                {
                    buffer = new byte[wrappedCodeLength];
                    COMMONJS_HEADER_UTF8.CopyTo(buffer, 0);
                }

                sourceCode.AsReadOnlySpan().CopyTo(buffer.AsSpan(COMMONJS_HEADER_UTF8.Length));
                COMMONJS_FOOTER_UTF8.CopyTo(buffer, COMMONJS_HEADER_UTF8.Length + sourceCode.Length);

                sceneScript = engine.CompileScriptFromUtf8(buffer.AsSpan(0, wrappedCodeLength));
            }

            var unityOpsApi = new UnityOpsApi(engine, moduleHub, sceneScript, sceneShortInfo);
            engine.AddHostObject("UnityOpsApi", unityOpsApi);

            Result<int> initCodeLengthResult = await LoadScriptAsync(
                Path.Combine(Application.streamingAssetsPath, "Js/Init.js"), buffer, 0);

            if (!initCodeLengthResult.Success)
                throw new Exception(initCodeLengthResult.ErrorMessage);

            engine.ExecuteScriptFromUtf8(buffer.AsSpan(0, initCodeLengthResult.Value));
            return new SceneRuntimeImpl(engine);
        }

        /// <summary>
        ///     Must be called on the main thread
        /// </summary>
        public async UniTask<SceneRuntimeImpl> CreateByPathAsync(
            URLAddress path,
            SceneShortInfo sceneShortInfo,
            CancellationToken ct,
            InstantiationBehavior instantiationBehavior = InstantiationBehavior.StayOnMainThread)
        {
            await EnsureCalledOnMainThreadAsync();

            Result<DownloadedOrCachedData> sourceCodeResult = await webJsSources.SceneSourceCodeAsync(
                path, ct);

            if (!sourceCodeResult.Success)
                throw new Exception(sourceCodeResult.ErrorMessage);

            using DownloadedOrCachedData sourceCode = sourceCodeResult.Value;
            return await CreateBySourceCodeAsync(sourceCode, sceneShortInfo, ct, instantiationBehavior);

            // DownloadHandler.Dispose is being called on a background thread at this point. Unity does
            // not seem to mind, but if that changes, this comment will help you.
        }

        private static async UniTask EnsureCalledOnMainThreadAsync()
        {
            if (!PlayerLoopHelper.IsMainThread)
            {
                ReportHub.Log(ReportCategory.SCENE_FACTORY, $"{nameof(CreateByPathAsync)} must be called on the main thread");
                await UniTask.SwitchToMainThread();
            }
        }

        private static async UniTask<Result<int>> LoadScriptAsync(string path, byte[] buffer, int offset)
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite);

            if (stream.Length > buffer.Length)
                Result<int>.ErrorResult(
                    $"File \"{path}\" is larger than the buffer ({buffer.Length} bytes)");

            int count = (int)stream.Length;
            Result readResult = await stream.ReadReliablyAsync(buffer, offset, count);

            if (!readResult.Success)
                return Result<int>.ErrorResult(readResult.ErrorMessage ?? "null");
            else
                return Result<int>.SuccessResult(count);
        }
    }
}
