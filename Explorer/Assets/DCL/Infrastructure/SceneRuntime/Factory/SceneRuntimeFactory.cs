using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization;
using DCL.Utility.Types;
using ECS;
using ECS.StreamableLoading.Cache.Disk;
using Microsoft.ClearScript.V8;
using SceneRuntime.Apis;
using SceneRuntime.Factory.JsSceneSourceCode;
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
            SlicedOwnedMemory<byte> sourceCode, SceneShortInfo sceneShortInfo, CancellationToken ct,
            InstantiationBehavior instantiationBehavior = InstantiationBehavior.StayOnMainThread)
        {
            await EnsureCalledOnMainThreadAsync();

            jsSourcesCache.Cache(
                $"{realmData.RealmName} {sceneShortInfo.BaseParcel.x},{sceneShortInfo.BaseParcel.y} {sceneShortInfo.Name}.js",
                sourceCode.Memory.Span
            );

            // On instantiation there is a bit of logic to execute by the scene runtime so we can benefit from the thread pool
            if (instantiationBehavior == InstantiationBehavior.SwitchToThreadPool)
                await UniTask.SwitchToThreadPool();

            // Provide basic Thread Pool synchronization context
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());

            V8ScriptEngine engine = engineFactory.Create(sceneShortInfo);
            var moduleHub = new SceneModuleHub(engine);

            // Look at StreamingAssets/Js, find the largest file in there, add a kilobyte on top, round
            // up, and that's the magic number.
            const int OUR_CODE_BUFFER_SIZE = 29000;

            using var ourCodeBuffer = new SlicedOwnedMemory<byte>(OUR_CODE_BUFFER_SIZE);
            COMMONJS_HEADER_UTF8.CopyTo(ourCodeBuffer.Memory);

            foreach (string moduleName in JS_MODULE_NAMES)
            {
                Result<int> moduleCodeLengthResult = await LoadScriptAsync(
                    Path.Combine(Application.streamingAssetsPath, "Js/Modules", moduleName),
                    ourCodeBuffer.Memory.Slice(COMMONJS_HEADER_UTF8.Length));

                if (!moduleCodeLengthResult.Success)
                    throw new Exception(moduleCodeLengthResult.ErrorMessage);

                if (ourCodeBuffer.Memory.Span
                          .Slice(COMMONJS_HEADER_UTF8.Length, COMMONJS_HEADER_UTF8.Length)
                          .SequenceEqual(COMMONJS_HEADER_UTF8))
                {
                    moduleHub.LoadAndCompileJsModule(moduleName, ourCodeBuffer.Memory.Span.Slice(
                        COMMONJS_HEADER_UTF8.Length, moduleCodeLengthResult.Value));
                }
                else
                {
                    COMMONJS_FOOTER_UTF8.CopyTo(ourCodeBuffer.Memory.Slice(
                        COMMONJS_HEADER_UTF8.Length + moduleCodeLengthResult.Value));

                    moduleHub.LoadAndCompileJsModule(moduleName, ourCodeBuffer.Memory.Span.Slice(0,
                        COMMONJS_HEADER_UTF8.Length + moduleCodeLengthResult.Value
                                                    + COMMONJS_FOOTER_UTF8.Length));
                }
            }

            V8Script sceneScript;

            if (sourceCode.Memory.Length >= COMMONJS_HEADER_UTF8.Length
                && sourceCode.Memory.Span
                             .Slice(0, COMMONJS_HEADER_UTF8.Length)
                             .SequenceEqual(COMMONJS_HEADER_UTF8))
                sceneScript = engine.CompileScriptFromUtf8(sourceCode.Memory.Span);
            else
            {
                ReportHub.LogWarning(ReportCategory.SCENE_FACTORY,
                    $"The code of the scene \"{sceneShortInfo.Name}\" at parcel {sceneShortInfo.BaseParcel} does not include the CommonJS module wrapper. This is suboptimal.");

                using var wrappedCode = new SlicedOwnedMemory<byte>(COMMONJS_HEADER_UTF8.Length
                                                                    + sourceCode.Memory.Length
                                                                    + COMMONJS_FOOTER_UTF8.Length);

                COMMONJS_HEADER_UTF8.CopyTo(wrappedCode.Memory);

                sourceCode.Memory.Span.CopyTo(
                    wrappedCode.Memory.Span.Slice(COMMONJS_HEADER_UTF8.Length));

                COMMONJS_FOOTER_UTF8.CopyTo(
                    wrappedCode.Memory.Span.Slice(COMMONJS_HEADER_UTF8.Length + sourceCode.Memory.Length));

                sceneScript = engine.CompileScriptFromUtf8(wrappedCode.Memory.Span);
            }

            var unityOpsApi = new UnityOpsApi(engine, moduleHub, sceneScript, sceneShortInfo);
            engine.AddHostObject("UnityOpsApi", unityOpsApi);

            Result<int> initCodeLengthResult = await LoadScriptAsync(
                Path.Combine(Application.streamingAssetsPath, "Js/Init.js"), ourCodeBuffer.Memory);

            if (!initCodeLengthResult.Success)
                throw new Exception(initCodeLengthResult.ErrorMessage);

            engine.ExecuteScriptFromUtf8(
                ourCodeBuffer.Memory.Span.Slice(0, initCodeLengthResult.Value));

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

            Result<SlicedOwnedMemory<byte>> sourceCodeResult = await webJsSources.SceneSourceCodeAsync(
                path, ct);

            if (!sourceCodeResult.Success)
                throw new Exception(sourceCodeResult.ErrorMessage);

            using SlicedOwnedMemory<byte> sourceCode = sourceCodeResult.Value;
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

        private static async UniTask<Result<int>> LoadScriptAsync(string path, Memory<byte> buffer)
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite);

            if (stream.Length > buffer.Length)
                Result<int>.ErrorResult(
                    $"File \"{path}\" is larger than the buffer ({buffer.Length} bytes)");

            int count = (int)stream.Length;
            Result readResult = await stream.ReadReliablyAsync(buffer.Slice(0, count));

            if (!readResult.Success)
                return Result<int>.ErrorResult(readResult.ErrorMessage ?? "null");
            else
                return Result<int>.SuccessResult(count);
        }
    }
}
