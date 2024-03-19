using CrdtEcsBridge.PoolsProviders;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.Web3;
using DCL.Web3.Identities;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using Microsoft.ClearScript.V8;
using SceneRunner.Scene.ExceptionsHandling;
using SceneRuntime.Apis;
using SceneRuntime.Apis.Modules;
using System;
using System.Collections.Generic;
using UnityEngine.Assertions;

namespace SceneRuntime
{
    // Avoid the same name for Namespace and Class
    public class SceneRuntimeImpl : ISceneRuntime, IJsOperations
    {
        internal readonly V8ScriptEngine engine;
        private readonly ISceneExceptionsHandler sceneExceptionsHandler;
        private readonly IInstancePoolsProvider instancePoolsProvider;

        private readonly SceneModuleLoader moduleLoader;
        private readonly UnityOpsApi unityOpsApi; // TODO: This is only needed for the LifeCycle
        private readonly ScriptObject sceneCode;
        private readonly ScriptObject updateFunc;
        private readonly ScriptObject startFunc;

        // ResetableSource is an optimization to reduce 11kb of memory allocation per Update (reduces 15kb to 4kb per update)
        private readonly JSTaskResolverResetable resetableSource;

        private EngineApiWrapper? engineApi;
        private EthereumApiWrapper? ethereumApi;
        private RuntimeWrapper? runtimeWrapper;
        private RestrictedActionsAPIWrapper restrictedActionsApi;
        private UserIdentityApiWrapper? userIdentity;
        private SceneApiWrapper? sceneApiWrapper;
        private CommunicationsControllerAPIWrapper communicationsControllerApi;

        public SceneRuntimeImpl(
            ISceneExceptionsHandler sceneExceptionsHandler,
            string sourceCode, string jsInitCode,
            Dictionary<string, string> jsModules,
            IInstancePoolsProvider instancePoolsProvider,
            SceneShortInfo sceneShortInfo)
        {
            this.sceneExceptionsHandler = sceneExceptionsHandler;
            this.instancePoolsProvider = instancePoolsProvider;
            resetableSource = new JSTaskResolverResetable();
            moduleLoader = new SceneModuleLoader();
            engine = V8EngineFactory.Create();

            // Compile Scene Code
            V8Script sceneScript = engine.Compile(sourceCode);

            // Initialize init API
            unityOpsApi = new UnityOpsApi(engine, moduleLoader, sceneScript, sceneShortInfo);
            engine.AddHostObject("UnityOpsApi", unityOpsApi);
            engine.Execute(jsInitCode);

            // Setup unitask resolver
            engine.AddHostObject("__resetableSource", resetableSource);

            // Load and Compile Js Modules
            moduleLoader.LoadAndCompileJsModules(engine, jsModules);

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

            updateFunc = (ScriptObject)engine.Evaluate("__internalOnUpdate");
            startFunc = (ScriptObject)engine.Evaluate("__internalOnStart");
        }

        public void Dispose()
        {
            engineApi?.Dispose();
            ethereumApi?.Dispose();
            userIdentity?.Dispose();
            engine.Dispose();
            runtimeWrapper?.Dispose();
            restrictedActionsApi?.Dispose();
            sceneApiWrapper?.Dispose();
            communicationsControllerApi?.Dispose();
        }

        public void RegisterEngineApi(IEngineApi api)
        {
            engine.AddHostObject("UnityEngineApi", engineApi = new EngineApiWrapper(api, instancePoolsProvider, sceneExceptionsHandler));
        }

        public void RegisterRuntime(IRuntime api)
        {
            engine.AddHostObject("UnityRuntime", runtimeWrapper = new RuntimeWrapper(api, sceneExceptionsHandler));
        }

        public void RegisterSceneApi(ISceneApi api)
        {
            engine.AddHostObject("UnitySceneApi", sceneApiWrapper = new SceneApiWrapper(api));
        }

        public void RegisterEthereumApi(IEthereumApi ethereumApi)
        {
            engine.AddHostObject("UnityEthereumApi", this.ethereumApi = new EthereumApiWrapper(ethereumApi, sceneExceptionsHandler));
        }

        public void RegisterRestrictedActionsApi(IRestrictedActionsAPI api)
        {
            engine.AddHostObject("UnityRestrictedActionsApi", restrictedActionsApi = new RestrictedActionsAPIWrapper(api));
        }

        public void RegisterUserIdentityApi(IProfileRepository profileRepository, IWeb3IdentityCache identityCache)
        {
            engine.AddHostObject("UnityUserIdentityApi", userIdentity = new UserIdentityApiWrapper(profileRepository, identityCache, sceneExceptionsHandler));
        }

        public void RegisterCommunicationsControllerApi(ICommunicationsControllerAPI api)
        {
            engine.AddHostObject("UnityCommunicationsControllerApi", communicationsControllerApi = new CommunicationsControllerAPIWrapper(api));
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
            PoolableByteArray result = engineApi.api.CrdtSendToRenderer(data, false);

            // Initial messages are not expected to return anything
            Assert.IsTrue(result.IsEmpty);
        }

        public ITypedArray<byte> CreateUint8Array(int length) =>
            (ITypedArray<byte>)engine.Evaluate("(function () { return new Uint8Array(" + length + "); })()");
    }
}
