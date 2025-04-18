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
using System.Buffers;
using System.Collections.Generic;
using UnityEngine.Assertions;
using Utility;

namespace SceneRuntime
{
    // Avoid the same name for Namespace and Class
    public sealed class SceneRuntimeImpl : ISceneRuntime, IJsOperations
    {
        internal readonly V8ScriptEngine engine;
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
            V8EngineFactory engineFactory
        )
        {
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

        /// <remarks>
        /// <see cref="SceneFacade"/> is a component in the global scene as an
        /// <see cref="ISceneFacade"/>. It owns its <see cref="SceneRuntimeImpl"/> through its
        /// <see cref="deps"/> field, which in turns owns its <see cref="V8ScriptEngine"/>. So that also
        /// shall be the chain of Dispose calls.
        /// </remarks>
        public void Dispose()
        {
            engine.Dispose();
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

        public V8RuntimeHeapInfo RuntimeHeapInfo { get; private set; }

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
            RuntimeHeapInfo = engine.GetRuntimeHeapInfo();
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

        public ITypedArray<byte> CreateUint8Array(int length) =>
            (ITypedArray<byte>)engine.Evaluate("(function () { return new Uint8Array(" + length + "); })()").EnsureNotNull();

        public ITypedArray<byte> CreateUint8Array(ReadOnlyMemory<byte> memory)
        {
            var jsArray = CreateUint8Array(memory.Length);
            if (!memory.IsEmpty)
                jsArray.Write(memory, (ulong)memory.Length, 0);
            return jsArray;
        }

        public ScriptObject ConvertToScriptTypedArrays(IReadOnlyList<IMemoryOwner<byte>> byteArrays)
        {
            var js2DArray = (ScriptObject) engine.Evaluate("[]"); // create an outer array

            // for every inner array create ITypedArray<byte>
            foreach (var innerArray in byteArrays)
            {
                var memory = innerArray.Memory;

                var innerJsArray = CreateUint8Array(memory.Length);

                // Call into JS to write the data via a pointer
                innerJsArray.Write(memory, (ulong)memory.Length, 0);

                // Push the new element to js2DArray
                js2DArray.InvokeMethod("push", innerJsArray);
            }

            return js2DArray;
        }
    }
}
