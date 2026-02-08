using CRDT.Serializer;
using CrdtEcsBridge.JsModulesImplementation.Communications;
using CrdtEcsBridge.PoolsProviders;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.Messaging.Hubs;

#if !NO_LIVEKIT_MODE
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Profiles.Poses;
#endif

using DCL.PluginSystem.World.Dependencies;
using DCL.Clipboard;
using MVC;
using DCL.Profiles;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS;
using Global.Dynamic;
using SceneRunner;
using SceneRunner.ECSWorld;
using SceneRuntime;
using SceneRuntime.Factory;
using SceneRuntime.Factory.WebSceneSource;
using Temp.Helper.WebClient;

#if UNITY_WEBGL
using SceneRuntime.WebClient;
#else
using SceneRuntime.V8;
#endif

namespace Global
{
    /// <summary>
    ///     This class is never stored in a field and goes out of scope at the end of
    ///     <see cref="Bootstrap.CreateGlobalWorld" />. Consequently, it does not own any code and is not in
    ///     fact a container.
    /// </summary>
    public class SceneSharedContainer
    {
        /// <summary>
        ///     Is actually owned by <see cref="ECS.SceneLifeCycle.Systems.LoadSceneSystem" />
        /// </summary>
        public ISceneFactory? SceneFactory { get; private set; }

        public static SceneSharedContainer Create(in StaticContainer staticContainer,
            IDecentralandUrlsSource decentralandUrlsSource,
            IWeb3IdentityCache web3IdentityCache,
            IWebRequestController webRequestController,
            IRealmData realmData,
            IProfileRepository profileRepository,

#if !NO_LIVEKIT_MODE
            IRoomHub roomHub,
#endif

            IMVCManager mvcManager,
            IMessagePipesHub messagePipesHub,

#if !NO_LIVEKIT_MODE
            IRemoteMetadata remoteMetadata,
#endif

            IWebJsSources webJsSources,
            DecentralandEnvironment dclEnvironment,
            ISystemClipboard systemClipboard)
        {
            WebGLDebugLog.Log("SceneSharedContainer.Create: start");
            ECSWorldSingletonSharedDependencies sharedDependencies = staticContainer.SingletonSharedDependencies;
            ExposedGlobalDataContainer exposedGlobalDataContainer = staticContainer.ExposedGlobalDataContainer;

            WebGLDebugLog.Log("SceneSharedContainer.Create: before ECSWorldFactory");
            var ecsWorldFactory = new ECSWorldFactory(sharedDependencies,
                staticContainer.PartitionSettings,
                exposedGlobalDataContainer.CameraSamplingData,
                staticContainer.ECSWorldPlugins);
            WebGLDebugLog.Log("SceneSharedContainer.Create: after ECSWorldFactory");

#if UNITY_WEBGL
            IJavaScriptEngineFactory engineFactory = new WebClientJavaScriptEngineFactory();
#else
            IJavaScriptEngineFactory engineFactory = new V8EngineFactory();
#endif

            WebGLDebugLog.Log("SceneSharedContainer.Create: before SceneFactory ctor");
            var sceneFactory = new SceneFactory(
                    ecsWorldFactory,
                    new SceneRuntimeFactory(realmData ?? new IRealmData.Fake(), engineFactory,
                        webJsSources),
                    new SharedPoolsProvider(),
                    new CRDTSerializer(),
                    staticContainer.ComponentsContainer.SDKComponentsRegistry,
                    sharedDependencies.EntityFactory,
                    staticContainer.EntityCollidersGlobalCache,
                    staticContainer.EthereumApi,
                    mvcManager,
                    profileRepository,
                    web3IdentityCache,
                    decentralandUrlsSource,
                    webRequestController,

#if !NO_LIVEKIT_MODE
                    roomHub,
#endif

                    realmData,
                    staticContainer.PortableExperiencesController,
                    staticContainer.StaticSettings.SkyboxSettings,
                    new SceneCommunicationPipe(
                            messagePipesHub

#if !NO_LIVEKIT_MODE
                            , roomHub.SceneRoom()
#endif

                            ),

#if !NO_LIVEKIT_MODE
                    remoteMetadata,
#endif

                    dclEnvironment,
                    systemClipboard);
            WebGLDebugLog.Log("SceneSharedContainer.Create: after SceneFactory ctor");
            return new SceneSharedContainer { SceneFactory = sceneFactory };
        }
    }
}
