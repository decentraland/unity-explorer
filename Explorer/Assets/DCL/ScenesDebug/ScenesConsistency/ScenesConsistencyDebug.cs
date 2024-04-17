using CommunicationData.URLHelpers;
using CRDT.Serializer;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.JsModulesImplementation.Communications;
using CrdtEcsBridge.PoolsProviders;
using Cysharp.Threading.Tasks;
using DCL.CharacterCamera;
using DCL.Diagnostics;
using DCL.Interaction.Utility;
using DCL.Ipfs;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Messaging.Pipe;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.PluginSystem.World;
using DCL.PluginSystem.World.Dependencies;
using DCL.Profiles;
using DCL.ScenesDebug.ScenesConsistency.Entities;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.Reporting;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.SceneLifeCycle.Systems;
using MVC;
using MVC.PopupsController.PopupCloser;
using SceneRunner;
using SceneRunner.ECSWorld;
using SceneRuntime.Factory;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace DCL.ScenesDebug.ScenesConsistency
{
    public class ScenesConsistencyDebug : MonoBehaviour
    {
        [SerializeField] private TextAsset scenesCoordinatesRaw;
        //[SerializeField] private
        [Header("Debug")]
        [SerializeField] private List<SceneEntity> entities;

        private static readonly URLDomain ASSET_BUNDLE_URL = URLDomain.FromString("https://ab-cdn.decentraland.org/");

        private ISceneFactory sceneFactory;
        private LoadSceneSystemLogic loadSceneSystemLogic;

        private void Start()
        {
            var identityCache = new IWeb3IdentityCache.Default();

            sceneFactory = new SceneFactory(
                new ECSWorldFactory(
                    new ECSWorldSingletonSharedDependencies(),
                    ScriptableObject.CreateInstance<PartitionSettingsAsset>(),
                    new CameraSamplingData(),
                    new IExposedCameraData.Random(),
                    new SceneReadinessReportQueue(
                        new ScenesCache()
                    ),
                    new List<IDCLWorldPlugin>()
                ),
                new SceneRuntimeFactory(IWebRequestController.DEFAULT),
                new SharedPoolsProvider(),
                new CRDTSerializer(),
                new SDKComponentsRegistry(),
                new SceneEntityFactory(),
                new EntityCollidersGlobalCache(),
                new DappWeb3Authenticator.Default(identityCache),
                new MVCManager(
                    new WindowStackManager(),
                    new CancellationTokenSource(),
                    new IPopupCloserView.Fake()
                ),
                new IProfileRepository.Fake(),
                identityCache,
                IWebRequestController.DEFAULT,
                new IRoomHub.Fake(),
                new RealmData(),
                new CommunicationControllerHub(
                    new MessagePipesHub(
                        new IMessagePipe.Null(),
                        new IMessagePipe.Null()
                    )
                )
            );

            loadSceneSystemLogic = new LoadSceneSystemLogic(IWebRequestController.DEFAULT, ASSET_BUNDLE_URL);

            LaunchAsync().Forget();
        }

        private async UniTaskVoid LaunchAsync()
        {
            entities = SceneEntities.FromText(scenesCoordinatesRaw)
                                    .Where(x => x.IsRunning() == false)
                                    .ToList();

            foreach (SceneEntity entity in entities)
            {
                var result = await loadSceneSystemLogic.FlowAsync(
                    sceneFactory,
                    new GetSceneFacadeIntention(
                        new LocalIpfsRealm(URLDomain.EMPTY),
                        new SceneDefinitionComponent(
                            new SceneEntityDefinition(
                                string.Empty,
                                new SceneMetadata()
                            ),
                            new IpfsPath()
                        )
                    ),
                    ReportCategory.UNSPECIFIED,
                    PartitionComponent.TOP_PRIORITY,
                    destroyCancellationToken
                );

                await result.StartUpdateLoopAsync(60, destroyCancellationToken);
            }
        }
    }
}
