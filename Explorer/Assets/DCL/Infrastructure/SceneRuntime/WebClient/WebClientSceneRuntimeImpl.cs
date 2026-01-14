using CrdtEcsBridge.PoolsProviders;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using SceneRuntime.Apis;
using SceneRuntime.Apis.Modules.EngineApi;
using SceneRuntime.ModuleHub;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Assertions;
using Utility;

namespace SceneRuntime.WebClient
{
    public sealed class WebClientSceneRuntimeImpl : ISceneRuntime
    {
        private readonly IJavaScriptEngine engine;
        private readonly WebClientScriptObject arrayCtor;
        private readonly WebClientScriptObject unit8ArrayCtor;
        private WebClientScriptObject updateFunc;
        private WebClientScriptObject startFunc;
        private readonly List<IDCLTypedArray<byte>> uint8Arrays;
        private readonly JsApiBunch jsApiBunch;

        private readonly JSTaskResolverResetable resetableSource;

        private readonly CancellationTokenSource isDisposingTokenSource = new ();
        private int nextUint8Array;
        private EngineApiWrapper? engineApi;

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
            var typedArrayConverter = new WebClientTypedArrayConverter();
            jsApiBunch = new JsApiBunch(engine, typedArrayConverter);

            var moduleHub = new SceneModuleHub(engine);

            moduleHub.LoadAndCompileJsModules(jsModules);

            ICompiledScript sceneScript = engine.Compile(sourceCode).EnsureNotNull();

            var unityOpsApi = new UnityOpsApi(engine, moduleHub, sceneScript, sceneShortInfo);
            engine.AddHostObject("UnityOpsApi", unityOpsApi);

            engine.Execute(initCode);

            engine.Execute("globalThis.ENABLE_SDK_TWEEN_SEQUENCE = false;");

            engine.AddHostObject("__resetableSource", resetableSource);

            arrayCtor = (WebClientScriptObject)engine.Global.GetProperty("Array");
            unit8ArrayCtor = (WebClientScriptObject)engine.Global.GetProperty("Uint8Array");
            uint8Arrays = new List<IDCLTypedArray<byte>>();
            nextUint8Array = 0;
        }

        public void Dispose()
        {
            engine.Dispose();
            jsApiBunch.Dispose();
        }

        public void ExecuteSceneJson()
        {
            engine.Execute(@"
            const __internalScene = require('~scene.js')
            const __internalOnStart = async function () {
                try {
                    await __internalScene.onStart()
                    __resetableSource.Completed()
                } catch (e) {
                    __resetableSource.Reject(e.stack)
                }
            }
            const __internalOnUpdate = async function (dt) {
                try {
                    await __internalScene.onUpdate(dt)
                    __resetableSource.Completed()
                } catch(e) {
                    __resetableSource.Reject(e.stack)
                }
            }
        ");

            updateFunc = (WebClientScriptObject)engine.Evaluate("__internalOnUpdate");
            startFunc = (WebClientScriptObject)engine.Evaluate("__internalOnStart");
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
            resetableSource.Reset();
            startFunc.InvokeAsFunction();
            return resetableSource.Task;
        }

        public UniTask UpdateScene(float dt)
        {
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
            object result = unit8ArrayCtor.Invoke(true, length);
            if (result is WebClientScriptObject webglScriptObject)
                return new WebClientTypedArrayAdapter(webglScriptObject);
            throw new InvalidCastException($"Expected WebGLScriptObject but got {result?.GetType()}");
        }

        IDCLTypedArray<byte> IJsOperations.GetTempUint8Array()
        {
            if (nextUint8Array >= uint8Arrays.Count)
            {
                object result = unit8ArrayCtor.Invoke(true, IJsOperations.LIVEKIT_MAX_SIZE);
                if (result is WebClientScriptObject webglScriptObject)
                    uint8Arrays.Add(new WebClientTypedArrayAdapter(webglScriptObject));
                else
                    throw new InvalidCastException($"Expected WebGLScriptObject but got {result?.GetType()}");
            }

            return uint8Arrays[nextUint8Array++];
        }
    }
}
