#if UNITY_WEBGL && (!UNITY_EDITOR || EDITOR_DEBUG_WEBGL)
using CrdtEcsBridge.PoolsProviders;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using SceneRuntime.Apis;
using SceneRuntime.Apis.Modules.EngineApi;
using SceneRuntime.ModuleHub;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine.Assertions;
using Utility;

namespace SceneRuntime.WebClient
{
    /// <summary>
    ///     WebGL implementation of <see cref="ISceneRuntime" /> that drives a Decentraland scene inside a browser-hosted
    ///     JavaScript engine (<see cref="WebClientJavaScriptEngine" />).
    ///     <para>
    ///         On construction it compiles and registers all JS modules (system libs + the scene script itself) with the engine
    ///         so that <c>require()</c> calls in Init.js resolve correctly. Actual Init.js execution is deferred until
    ///         <see cref="ExecuteSceneJson" /> runs, which guarantees all host API objects are registered first.
    ///     </para>
    ///     <para>
    ///         The update loop calls <c>onStart</c> / <c>onUpdate</c> on the compiled scene module and synchronises
    ///         completion via a <see cref="JSTaskResolverResetable" /> that is polled by the JS promise chain.
    ///     </para>
    ///     <para>Only compiled for <c>UNITY_WEBGL</c> builds (or <c>EDITOR_DEBUG_WEBGL</c> in the editor).</para>
    /// </summary>
    public sealed class WebClientSceneRuntimeImpl : ISceneRuntime
    {
        private readonly IJavaScriptEngine engine;
        private readonly WebClientScriptObject arrayCtor;
        private readonly WebClientScriptObject uint8ArrayCtor;
        private WebClientScriptObject updateFunc;
        private WebClientScriptObject startFunc;
        private readonly List<IDCLTypedArray<byte>> uint8Arrays;
        private readonly JsApiBunch jsApiBunch;

        private readonly JSTaskResolverResetable resetableSource;

        private readonly CancellationTokenSource isDisposingTokenSource = new ();
        private int nextUint8Array;
        private EngineApiWrapper? engineApi;
        private readonly string initCode;
        private bool initCodeExecuted;

        public IRuntimeHeapInfo? RuntimeHeapInfo { get; private set; }

        CancellationTokenSource ISceneRuntime.IsDisposingTokenSource => isDisposingTokenSource;

        public WebClientSceneRuntimeImpl(
            string sourceCode,
            string initCode,
            IReadOnlyDictionary<string, string> jsModules,
            SceneShortInfo sceneShortInfo,
            IJavaScriptEngineFactory engineFactory
        )
        {
            resetableSource = new JSTaskResolverResetable();
            engine = engineFactory.Create(sceneShortInfo);

            // Cast to WebClientJavaScriptEngine to access RegisterModule
            var webClientEngine = engine as WebClientJavaScriptEngine;
            if (webClientEngine == null)
                throw new InvalidOperationException("WebClientSceneRuntimeImpl requires a WebClientJavaScriptEngine");

            var typedArrayConverter = new WebClientTypedArrayConverter();
            jsApiBunch = new JsApiBunch(engine, typedArrayConverter);

            var moduleHub = new SceneModuleHub(engine);

            // Load, compile, and register JS modules
            LoadCompileAndRegisterModules(webClientEngine, moduleHub, jsModules);

            // Compile and register the scene script
            ICompiledScript sceneScript = engine.Compile(sourceCode).EnsureNotNull();

            // Register the scene script so JavaScript can look it up via require('~scene.js')
            if (sceneScript is WebGLCompiledScript webglSceneScript)
                webClientEngine.RegisterModule("~scene.js", webglSceneScript.ScriptId);

            // Add UnityOpsApi as a host object (the jslib proxy handles its methods directly)
            var unityOpsApi = new UnityOpsApi(engine, moduleHub, sceneScript, sceneShortInfo);
            engine.AddHostObject("UnityOpsApi", unityOpsApi);

            // Defer init code until ExecuteSceneJson(), which runs after RegisterAll().
            // Init and required modules (e.g. WebSocketApi) expect UnityWebSocketApi and other host APIs to be already registered.
            this.initCode = initCode;
            initCodeExecuted = false;

            engine.AddHostObject("__resetableSource", resetableSource);

            arrayCtor = (WebClientScriptObject)engine.Global.GetProperty("Array");
            uint8ArrayCtor = (WebClientScriptObject)engine.Global.GetProperty("Uint8Array");
            uint8Arrays = new List<IDCLTypedArray<byte>>();
            nextUint8Array = 0;
        }

        /// <summary>
        /// Loads, compiles, and registers JS modules with the JavaScript engine.
        /// This ensures modules are available for lookup when require() is called.
        /// </summary>
        private void LoadCompileAndRegisterModules(
            WebClientJavaScriptEngine webClientEngine,
            SceneModuleHub moduleHub,
            IReadOnlyDictionary<string, string> jsModules)
        {
            foreach (KeyValuePair<string, string> source in jsModules)
            {
                ICompiledScript script = engine.Compile(source.Value);
                var moduleName = $"system/{source.Key}";
                string extension = Path.GetExtension(moduleName);

                // Get the script ID for registration
                string scriptId = (script as WebGLCompiledScript)?.ScriptId ?? "";
                if (string.IsNullOrEmpty(scriptId))
                {
                    ReportHub.Log(ReportCategory.WEB_CLIENT, $"Skipping module registration: compiled script for '{moduleName}' is not a WebGLCompiledScript or has no script ID.");
                    continue;
                }

                // Register with the ~ prefix that Init.js uses for require()
                // e.g., require('~system/WebSocketApi') -> module name "~system/WebSocketApi"
                webClientEngine.RegisterModule($"~{moduleName}", scriptId);

                // Also register without extension
                if (!string.IsNullOrEmpty(extension))
                {
                    string moduleNameWithoutExt = moduleName[..^extension.Length];
                    webClientEngine.RegisterModule($"~{moduleNameWithoutExt}", scriptId);
                }

                // Special cases for buffer and long (third-party library compatibility)
                if (source.Key == "buffer.js")
                {
                    webClientEngine.RegisterModule("~buffer", scriptId);
                    webClientEngine.RegisterModule("buffer", scriptId);
                }
                else if (source.Key == "long.js")
                {
                    webClientEngine.RegisterModule("~long", scriptId);
                    webClientEngine.RegisterModule("long", scriptId);
                }
            }

            // Also call the moduleHub's method to maintain the internal lookup for UnityOpsApi
            // (even though we handle LoadAndEvaluateCode in the jslib proxy now)
            moduleHub.LoadAndCompileJsModules(jsModules);
        }

        public void Dispose()
        {
            if (!isDisposingTokenSource.IsCancellationRequested)
            {
                isDisposingTokenSource.Cancel();
                isDisposingTokenSource.Dispose();
            }

            engine.Dispose();
            jsApiBunch.Dispose();
        }

        public void ExecuteSceneJson()
        {
            if (!initCodeExecuted)
            {
                engine.Execute(initCode);
                engine.Execute("globalThis.ENABLE_SDK_TWEEN_SEQUENCE = false;");
                initCodeExecuted = true;
            }

            // Use globalThis assignments instead of const so variables are accessible in later Evaluate calls
            engine.Execute(@"
            globalThis.__internalScene = require('~scene.js')
            globalThis.__internalOnStart = async function () {
                try {
                    await globalThis.__internalScene.onStart()
                    __resetableSource.Completed()
                } catch (e) {
                    __resetableSource.Reject(e.stack)
                }
            }
            globalThis.__internalOnUpdate = function (dt) {
                try {
                    globalThis.__internalScene.onUpdate(dt)
                    __resetableSource.Completed()
                } catch(e) {
                    __resetableSource.Reject(e.stack || String(e))
                }
            }
        ");

            updateFunc = (WebClientScriptObject)engine.Evaluate("globalThis.__internalOnUpdate");
            startFunc = (WebClientScriptObject)engine.Evaluate("globalThis.__internalOnStart");
        }

        public void OnSceneIsCurrentChanged(bool isCurrent)
        {
            jsApiBunch.OnSceneIsCurrentChanged(isCurrent);
        }

        public void RegisterEngineAPIWrapper(EngineApiWrapper newWrapper)
        {
            engineApi = newWrapper;
        }

        public void Register<T>(string itemName, T target) where T: JsApiWrapper
        {
            jsApiBunch.AddHostObject(itemName, target);
        }

        public void SetIsDisposing()
        {
            isDisposingTokenSource.Cancel();
            isDisposingTokenSource.Dispose();
        }

        public UniTask StartScene()
        {
            if (isDisposingTokenSource.IsCancellationRequested)
                return UniTask.CompletedTask;

            resetableSource.Reset();
            startFunc.InvokeAsFunction();
            return resetableSource.Task;
        }

        public UniTask UpdateScene(float dt)
        {
            if (isDisposingTokenSource.IsCancellationRequested)
                return UniTask.CompletedTask;

            nextUint8Array = 0;
            IRuntimeHeapInfo? heapInfo = engine.GetRuntimeHeapInfo();
            RuntimeHeapInfo = heapInfo;
            resetableSource.Reset();
            updateFunc.InvokeAsFunction(dt);
            return resetableSource.Task;
        }

        public void ApplyStaticMessages(ReadOnlyMemory<byte> data)
        {
            PoolableByteArray result = engineApi.EnsureNotNull().api.CrdtSendToRenderer(data, false);

            Assert.IsTrue(result.IsEmpty);
        }

        IDCLScriptObject IJsOperations.NewArray()
        {
            object result = arrayCtor.Invoke(true);
            WebClientScriptObject webglResult = (WebClientScriptObject)result;
            return webglResult;
        }

        IDCLTypedArray<byte> IJsOperations.NewUint8Array(int length)
        {
            object result = uint8ArrayCtor.Invoke(true, length);
            if (result is WebClientScriptObject webglScriptObject)
                return new WebClientTypedArrayAdapter(webglScriptObject);
            throw new InvalidCastException($"Expected WebGLScriptObject but got {result?.GetType()}");
        }

        IDCLTypedArray<byte> IJsOperations.GetTempUint8Array()
        {
            if (nextUint8Array >= uint8Arrays.Count)
            {
                object result = uint8ArrayCtor.Invoke(true, IJsOperations.LIVEKIT_MAX_SIZE);
                if (result is WebClientScriptObject webglScriptObject)
                    uint8Arrays.Add(new WebClientTypedArrayAdapter(webglScriptObject));
                else
                    throw new InvalidCastException($"Expected WebGLScriptObject but got {result?.GetType()}");
            }

            return uint8Arrays[nextUint8Array++];
        }
    }
}

#endif
