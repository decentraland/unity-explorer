using CRDT.Serializer;
using CrdtEcsBridge.JsModulesImplementation.Communications;
using CrdtEcsBridge.PoolsProviders;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.PluginSystem.World.Dependencies;
using MVC;
using DCL.Profiles;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS;
using SceneRunner;
using SceneRunner.ECSWorld;
using SceneRuntime.Factory;

namespace Global
{
    /// <summary>
    ///     Holds dependencies shared between all scene instances. <br />
    ///     Consider breaking down this class as much as needed if the number of dependencies grows
    /// </summary>
    public class SceneSharedContainer
    {
        public ISceneFactory SceneFactory { get; private set; }

        public static SceneSharedContainer Create(in StaticContainer staticContainer,
            IMVCManager mvcManager,
            IWeb3IdentityCache web3IdentityCache,
            IProfileRepository profileRepository,
            IWebRequestController webRequestController,
            IRoomHub roomHub,
            IRealmData? realmData,
            IMessagePipesHub messagePipesHub)
        {
            ECSWorldSingletonSharedDependencies sharedDependencies = staticContainer.SingletonSharedDependencies;
            ExposedGlobalDataContainer exposedGlobalDataContainer = staticContainer.ExposedGlobalDataContainer;

            var ecsWorldFactory = new ECSWorldFactory(sharedDependencies,
                staticContainer.PartitionSettings,
                exposedGlobalDataContainer.CameraSamplingData,
                exposedGlobalDataContainer.ExposedCameraData,
                staticContainer.SceneReadinessReportQueue,
                staticContainer.ECSWorldPlugins);

            return new SceneSharedContainer
            {
                SceneFactory = new SceneFactory(
                    ecsWorldFactory,
                    new SceneRuntimeFactory(staticContainer.WebRequestsContainer.WebRequestController),
                    new SharedPoolsProvider(),
                    new CRDTSerializer(),
                    staticContainer.ComponentsContainer.SDKComponentsRegistry,
                    sharedDependencies.EntityFactory,
                    staticContainer.EntityCollidersGlobalCache,
                    staticContainer.EthereumApi,
                    mvcManager,
                    profileRepository,
                    web3IdentityCache,
                    webRequestController,
                    roomHub,
                    realmData,
                    new CommunicationControllerHub(messagePipesHub)
                ),
            };
        }
    }
}
