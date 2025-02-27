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
using UnityEngine.Assertions;

namespace SceneRuntime
{
    // Avoid the same name for Namespace and Class
    public class SceneRuntimeImpl : ISceneRuntime, IJsOperations
    {
        internal readonly V8ScriptEngine engine;
        private readonly SceneShortInfo sceneShortInfo;
        private readonly V8ActiveEngines activeEngines;
        private readonly ScriptObject arrayCtor;
        private readonly ScriptObject unit8ArrayCtor;
        private readonly List<ITypedArray<byte>> uint8Arrays;
        private int nextUint8Array;
        private readonly JsApiBunch jsApiBunch;

        // ResetableSource is an optimization to reduce 11kb of memory allocation per Update (reduces 15kb to 4kb per update)
        private readonly JSTaskResolverResetable resetableSource;

        private ScriptObject updateFunc;
        private ScriptObject startFunc;
        private EngineApiWrapper? engineApi;

        public SceneRuntimeImpl(
            string sourceCode,
            (string validateCode, string jsInitCode) initCode,
            IReadOnlyDictionary<string, string> jsModules,
            SceneShortInfo sceneShortInfo,
            V8EngineFactory engineFactory,
            V8ActiveEngines activeEngines
        )
        {
            this.sceneShortInfo = sceneShortInfo;
            this.activeEngines = activeEngines;
            resetableSource = new JSTaskResolverResetable();

            engine = engineFactory.Create(sceneShortInfo);
            jsApiBunch = new JsApiBunch(engine);

            var moduleHub = new SceneModuleHub(engine);

            moduleHub.LoadAndCompileJsModules(jsModules);

            // Compile Scene Code
            V8Script sceneScript = engine.Compile(sourceCode).EnsureNotNull();

            // Initialize init API
            // TODO: This is only needed for the LifeCycle
            var unityOpsApi = new UnityOpsApi(engine, moduleHub, sceneScript, sceneShortInfo);
            engine.AddHostObject("UnityOpsApi", unityOpsApi);

            // engine.Execute(initCode.validateCode!);
            engine.Execute(initCode.jsInitCode!);

            // Setup unitask resolver
            engine.AddHostObject("__resetableSource", resetableSource);

            arrayCtor = (ScriptObject)engine.Global.GetProperty("Array");
            unit8ArrayCtor = (ScriptObject)engine.Global.GetProperty("Uint8Array");
            uint8Arrays = new ();
            nextUint8Array = 0;
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

            updateFunc = (ScriptObject)engine.Evaluate("__internalOnUpdate").EnsureNotNull();
            startFunc = (ScriptObject)engine.Evaluate("__internalOnStart").EnsureNotNull();
        }

        public void Dispose()
        {
            activeEngines.TryRemove(sceneShortInfo, engine);
            jsApiBunch.Dispose();
        }

        public void OnSceneIsCurrentChanged(bool isCurrent)
        {
            jsApiBunch.OnSceneIsCurrentChanged(isCurrent);
        }

        public void RegisterEngineAPIWrapper(EngineApiWrapper newWrapper)
        {
            engineApi = newWrapper;
        }

        public void Register<T>(string itemName, T target) where T: IJsApiWrapper
        {
            jsApiBunch.AddHostObject(itemName, target);
        }

        public void SetIsDisposing()
        {
            jsApiBunch.SetIsDisposing();
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
            resetableSource.Reset();
            updateFunc.InvokeAsFunction(dt);
            return resetableSource.Task;
        }

        public void ApplyStaticMessages(ReadOnlyMemory<byte> data)
        {
            PoolableByteArray result = engineApi.EnsureNotNull().api.CrdtSendToRenderer(data, false);

            // Initial messages are not expected to return anything
            Assert.IsTrue(result.IsEmpty);
        }

        ScriptObject IJsOperations.NewArray() =>
            (ScriptObject)arrayCtor.Invoke(true);

        ITypedArray<byte> IJsOperations.NewUint8Array(int length) =>
            (ITypedArray<byte>)unit8ArrayCtor.Invoke(true, length);

        ITypedArray<byte> IJsOperations.GetTempUint8Array()
        {
            if (nextUint8Array >= uint8Arrays.Count)
                uint8Arrays.Add((ITypedArray<byte>)unit8ArrayCtor.Invoke(true,
                    IJsOperations.LIVEKIT_MAX_SIZE));

            return uint8Arrays[nextUint8Array++];
        }
    }
}
