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

namespace SceneRuntime.Web
{
    public sealed class WebGLSceneRuntimeImpl : ISceneRuntime, IJsOperations
    {
        private readonly IJavaScriptEngine engine;
        private readonly WebGLScriptObject arrayCtor;
        private readonly WebGLScriptObject unit8ArrayCtor;
        private WebGLScriptObject updateFunc;
        private WebGLScriptObject startFunc;
        private readonly List<IDCLTypedArray<byte>> uint8Arrays;
        private readonly JsApiBunch jsApiBunch;

        private readonly JSTaskResolverResetable resetableSource;

        private readonly CancellationTokenSource isDisposingTokenSource = new ();
        private int nextUint8Array;
        private EngineApiWrapper? engineApi;

        public IRuntimeHeapInfo? RuntimeHeapInfo { get; private set; }

        CancellationTokenSource ISceneRuntime.IsDisposingTokenSource => isDisposingTokenSource;

        public WebGLSceneRuntimeImpl(
            string sourceCode,
            string initCode,
            IReadOnlyDictionary<string, string> jsModules,
            SceneShortInfo sceneShortInfo,
            IJavaScriptEngineFactory engineFactory
        )
        {
            resetableSource = new JSTaskResolverResetable();

            engine = engineFactory.Create(sceneShortInfo);
            jsApiBunch = new JsApiBunch(engine);

            var moduleHub = new SceneModuleHub(engine);

            moduleHub.LoadAndCompileJsModules(jsModules);

            ICompiledScript sceneScript = engine.Compile(sourceCode).EnsureNotNull();

            var unityOpsApi = new UnityOpsApi(engine, moduleHub, sceneScript, sceneShortInfo);
            engine.AddHostObject("UnityOpsApi", unityOpsApi);

            engine.Execute(initCode);

            engine.Execute("globalThis.ENABLE_SDK_TWEEN_SEQUENCE = false;");

            engine.AddHostObject("__resetableSource", resetableSource);

            arrayCtor = (WebGLScriptObject)engine.Global.GetProperty("Array");
            unit8ArrayCtor = (WebGLScriptObject)engine.Global.GetProperty("Uint8Array");
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

            updateFunc = (WebGLScriptObject)engine.Evaluate("__internalOnUpdate");
            startFunc = (WebGLScriptObject)engine.Evaluate("__internalOnStart");
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

        IScriptObject IJsOperations.NewArray()
        {
            IScriptObject result = arrayCtor.Invoke(true);
            WebGLScriptObject webglResult = (WebGLScriptObject)result;
            return webglResult;
        }

        IDCLTypedArray<byte> IJsOperations.NewUint8Array(int length)
        {
            object result = unit8ArrayCtor.Invoke(true, length);
            return (IDCLTypedArray<byte>)result;
        }

        IDCLTypedArray<byte> IJsOperations.GetTempUint8Array()
        {
            if (nextUint8Array >= uint8Arrays.Count)
            {
                object result = unit8ArrayCtor.Invoke(true, IJsOperations.LIVEKIT_MAX_SIZE);
                uint8Arrays.Add((IDCLTypedArray<byte>)result);
            }

            return uint8Arrays[nextUint8Array++];
        }
    }
}
