using CrdtEcsBridge.PoolsProviders;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using Microsoft.ClearScript.V8;
using SceneRunner.Scene.ExceptionsHandling;
using SceneRuntime.Apis;
using SceneRuntime.Apis.Modules;
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
        private readonly IInstancePoolsProvider instancePoolsProvider;

        private readonly ScriptObject updateFunc;
        private readonly ScriptObject startFunc;
        private readonly JsApiBunch jsApiBunch;

        // ResetableSource is an optimization to reduce 11kb of memory allocation per Update (reduces 15kb to 4kb per update)
        private readonly JSTaskResolverResetable resetableSource;

        private EngineApiWrapper? engineApi;

        public SceneRuntimeImpl(
            string sourceCode,
            (string validateCode, string jsInitCode) initCode,
            IReadOnlyDictionary<string, string> jsModules,
            IInstancePoolsProvider instancePoolsProvider,
            SceneShortInfo sceneShortInfo
        )
        {
            this.instancePoolsProvider = instancePoolsProvider;
            resetableSource = new JSTaskResolverResetable();
            engine = V8EngineFactory.Create();
            jsApiBunch = new JsApiBunch(engine);

            var moduleHub = new SceneModuleHub(engine);

            moduleHub.LoadAndCompileJsModules(jsModules);

            // Compile Scene Code
            V8Script sceneScript = engine.Compile(sourceCode).EnsureNotNull();

            // Load and Compile Js Modules
            moduleLoader.LoadAndCompileJsModules(engine, jsModules);

            // Initialize init API
            // TODO: This is only needed for the LifeCycle
            var unityOpsApi = new UnityOpsApi(engine, moduleHub, sceneScript, sceneShortInfo);
            engine.AddHostObject("UnityOpsApi", unityOpsApi);

            engine.Execute(initCode.validateCode!);
            engine.Execute(initCode.jsInitCode!);

            // Setup unitask resolver
            engine.AddHostObject("__resetableSource", resetableSource);

            // Load and Compile Js Modules

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
            engineApi?.Dispose();
            engine.Dispose();
            jsApiBunch.Dispose();
        }

        public void Register<T>(string itemName, T target) where T: IDisposable
        {
            jsApiBunch.AddHostObject(itemName, target);
        }

        public void RegisterEngineApi(IEngineApi api, ISceneExceptionsHandler sceneExceptionsHandler)
        {
            Register("UnityEngineApi", engineApi = new EngineApiWrapper(api, instancePoolsProvider, sceneExceptionsHandler));
        }

        public void RegisterWebSocketApi(IWebSocketApi webSocketApi)
        {
            engine.AddHostObject("UnityWebSocketApi", webSocketApiWrapper = new WebSocketApiWrapper(webSocketApi, sceneExceptionsHandler));
        }

        public void RegisterSimpleFetchApi(ISimpleFetchApi simpleFetchApi, IWebRequestController webRequestController)
        {
            engine.AddHostObject("UnitySimpleFetchApi", simpleFetchApiWrapper = new SimpleFetchApiWrapper(simpleFetchApi, webRequestController));
        }

        public void SetIsDisposing()
        {
            engineApi?.SetIsDisposing();
        }

        public UniTask StartScene()
        {
            resetableSource.Reset();
            startFunc.InvokeAsFunction();
            return resetableSource.Task;
        }

        public UniTask UpdateScene(float dt)
        {
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
    }
}
