using CRDT.Serializer;
using CrdtEcsBridge.JsModulesImplementation.Communications;
using CrdtEcsBridge.PoolsProviders;
using DCL.PluginSystem.World;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Profiles.Poses;
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
using System.Collections.Generic;
using System.Linq;

namespace Global
{
    /// <summary>
    /// This class is never stored in a field and goes out of scope at the end of
    /// <see cref="Bootstrap.CreateGlobalWorld"/>. Consequently, it does not own any code and is not in
    /// fact a container.
    /// </summary>
    public class SceneSharedContainer
    {
        /// <summary>
        /// Is actually owned by <see cref="ECS.SceneLifeCycle.Systems.LoadSceneSystem"/>
        /// </summary>
        public ISceneFactory SceneFactory { get; private set; }

        public static SceneSharedContainer Create(in StaticContainer staticContainer,
            IDecentralandUrlsSource decentralandUrlsSource,
            IWeb3IdentityCache web3IdentityCache,
            IWebRequestController webRequestController,
            IRealmData realmData,
            IProfileRepository profileRepository,
            IRoomHub roomHub,
            IMVCManager mvcManager,
            ISceneCommunicationPipe sceneCommunicationPipe,
            IRemoteMetadata remoteMetadata,
            IWebJsSources webJsSources,
            DecentralandEnvironment dclEnvironment,
            ISystemClipboard systemClipboard,
            IReadOnlyList<IDCLWorldPlugin> additionalWorldPlugins)
        {
            ECSWorldSingletonSharedDependencies sharedDependencies = staticContainer.SingletonSharedDependencies;
            ExposedGlobalDataContainer exposedGlobalDataContainer = staticContainer.ExposedGlobalDataContainer;

            var systemsDependencies = new SystemsDependencies(roomHub, staticContainer.EntityCollidersGlobalCache);

            var ecsWorldFactory = new ECSWorldFactory(sharedDependencies,
                systemsDependencies,
                staticContainer.PartitionSettings,
                exposedGlobalDataContainer.CameraSamplingData,
                staticContainer.ECSWorldPlugins.Concat(additionalWorldPlugins).ToArray());

            return new SceneSharedContainer
            {
                SceneFactory = new SceneFactory(
                    ecsWorldFactory,
                    new SceneRuntimeFactory(realmData ?? new IRealmData.Fake(), new V8EngineFactory(),
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
                    roomHub,
                    realmData,
                    staticContainer.PortableExperiencesController,
                    staticContainer.StaticSettings.SkyboxSettings,
                    sceneCommunicationPipe,
                    remoteMetadata,
                    dclEnvironment,
                    systemClipboard,
                    staticContainer.StaticSettings.BuildData?.InstallSource ?? string.Empty),
            };
        }
    }
}
