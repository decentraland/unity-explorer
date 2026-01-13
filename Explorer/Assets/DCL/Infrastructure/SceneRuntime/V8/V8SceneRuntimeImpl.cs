using CrdtEcsBridge.PoolsProviders;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using Microsoft.ClearScript.V8;
using SceneRuntime.Apis;
using SceneRuntime.Apis.Modules.EngineApi;
using SceneRuntime.ModuleHub;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Assertions;
using Utility;

namespace SceneRuntime.V8
{
    public sealed class V8SceneRuntimeImpl : ISceneRuntime
    {
        private readonly V8JavaScriptEngineAdapter engineAdapter;
        private readonly ScriptObject arrayCtor;
        private readonly ScriptObject unit8ArrayCtor;
        private readonly List<IDCLTypedArray<byte>> uint8Arrays;
        private readonly JsApiBunch jsApiBunch;
        private readonly JSTaskResolverResetable resetableSource;
        private readonly CancellationTokenSource isDisposingTokenSource = new ();

        private int nextUint8Array;
        private EngineApiWrapper? engineApi;
        private ScriptObject updateFunc;
        private ScriptObject startFunc;

        public V8ScriptEngine V8Engine => engineAdapter.V8Engine;
        public IRuntimeHeapInfo? RuntimeHeapInfo { get; private set; }

        CancellationTokenSource ISceneRuntime.IsDisposingTokenSource => isDisposingTokenSource;

        public V8SceneRuntimeImpl(
            string sourceCode,
            string initCode,
            IReadOnlyDictionary<string, string> jsModules,
            SceneShortInfo sceneShortInfo,
            IJavaScriptEngineFactory engineFactory
        )
        {
            resetableSource = new JSTaskResolverResetable();

            engineAdapter = (V8JavaScriptEngineAdapter)engineFactory.Create(sceneShortInfo);
            jsApiBunch = new JsApiBunch(engineAdapter);

            var moduleHub = new SceneModuleHub(engineAdapter);

            moduleHub.LoadAndCompileJsModules(jsModules);

            // Compile Scene Code
            ICompiledScript sceneScript = engineAdapter.Compile(sourceCode).EnsureNotNull();

            // Initialize init API
            // TODO: This is only needed for the LifeCycle
            var unityOpsApi = new UnityOpsApi(engineAdapter, moduleHub, sceneScript, sceneShortInfo);
            engineAdapter.AddHostObject("UnityOpsApi", unityOpsApi);

            engineAdapter.Execute(initCode);

            // Set global SDK configuration flags
            engineAdapter.Execute("globalThis.ENABLE_SDK_TWEEN_SEQUENCE = false;");

            // Setup unitask resolver
            engineAdapter.AddHostObject("__resetableSource", resetableSource);

            arrayCtor = (ScriptObject)engineAdapter.Global.GetProperty("Array");
            unit8ArrayCtor = (ScriptObject)engineAdapter.Global.GetProperty("Uint8Array");
            uint8Arrays = new List<IDCLTypedArray<byte>>();
            nextUint8Array = 0;
        }

        public void Dispose()
        {
            engineAdapter.Dispose();
            jsApiBunch.Dispose();
        }

        public void ExecuteSceneJson()
        {
            engineAdapter.Execute(@"
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

            updateFunc = (ScriptObject)engineAdapter.Evaluate("__internalOnUpdate").EnsureNotNull();
            startFunc = (ScriptObject)engineAdapter.Evaluate("__internalOnStart").EnsureNotNull();
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
            RuntimeHeapInfo = (V8RuntimeHeapInfoAdapter)engineAdapter.GetRuntimeHeapInfo();
            resetableSource.Reset();
            updateFunc.InvokeAsFunction(dt);
            return resetableSource.Task;
        }

        public void ApplyStaticMessages(ReadOnlyMemory<byte> data)
        {
            PoolableByteArray result = engineApi.EnsureNotNull().api.CrdtSendToRenderer(data, false);
            Assert.IsTrue(result.IsEmpty);
        }

        IDCLScriptObject IJsOperations.NewArray() =>
            new V8ScriptObjectAdapter((ScriptObject)arrayCtor.Invoke(true));

        IDCLTypedArray<byte> IJsOperations.NewUint8Array(int length) =>
            new V8TypedArrayAdapter((ITypedArray<byte>)unit8ArrayCtor.Invoke(true, length));

        IDCLTypedArray<byte> IJsOperations.GetTempUint8Array()
        {
            if (nextUint8Array >= uint8Arrays.Count)
            {
                var result = (ITypedArray<byte>)unit8ArrayCtor.Invoke(true, IJsOperations.LIVEKIT_MAX_SIZE);
                uint8Arrays.Add(new V8TypedArrayAdapter(result));
            }

            return uint8Arrays[nextUint8Array++];
        }
    }
}
