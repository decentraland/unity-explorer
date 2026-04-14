using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Ipfs;
using DCL.RealmNavigation;
using DCL.Utilities;
using ECS;
using ECS.LifeCycle;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Reporting;
using Global.AppArgs;
using Global.Dynamic;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.UserInAppInitializationFlow.Tests
{
    [TestFixture]
    public class TeleportStartupOperationShould
    {
        private World world;
        private ObjectProxy<Entity> cameraEntityProxy;
        private GlobalWorld globalWorld;
        private IGlobalRealmController realmController;
        private IRealmData realmData;
        private ITeleportController teleportController;
        private ILoadingStatus loadingStatus;
        private IAppArgs appArgs;
        private CancellationTokenSource cts;
        private CancellationTokenSource globalWorldCts;
        private WorldManifest worldManifest;

        [SetUp]
        public void SetUp()
        {
            world = World.Create();
            Entity cameraEntity = world.Create();
            cameraEntityProxy = new ObjectProxy<Entity>();
            cameraEntityProxy.SetObject(cameraEntity);

            globalWorldCts = new CancellationTokenSource();
            globalWorld = new GlobalWorld(world, null!, System.Array.Empty<IFinalizeWorldSystem>(),
                new CameraSamplingData(), new RealmSamplingData(), globalWorldCts);

            loadingStatus = Substitute.For<ILoadingStatus>();
            loadingStatus.SetCurrentStage(Arg.Any<LoadingStatus.LoadingStage>()).Returns(0.5f);

            realmData = Substitute.For<IRealmData>();
            realmData.RealmType.Returns(new ReactiveProperty<RealmKind>(RealmKind.World));
            worldManifest = WorldManifest.Empty;
            realmData.WorldManifest.Returns(worldManifest);

            realmController = Substitute.For<IGlobalRealmController>();
            realmController.RealmData.Returns(realmData);
            realmController.GlobalWorld.Returns(globalWorld);
            realmController.WaitForFixedScenePromisesAsync(Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(new List<SceneEntityDefinition>()));

            teleportController = Substitute.For<ITeleportController>();
            teleportController
                .TeleportToSceneSpawnPointAsync(Arg.Any<Vector2Int>(), Arg.Any<AsyncLoadProcessReport>(), Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult<WaitForSceneReadiness?>(null));

            appArgs = Substitute.For<IAppArgs>();
            cts = new CancellationTokenSource();
        }

        [TearDown]
        public void TearDown()
        {
            worldManifest.Dispose();
            world.Dispose();
            cts.Dispose();
            globalWorldCts.Dispose();
        }

        [Test]
        public void UsesManifestSpawnWhenNoPositionArgAndWorldManifestExists()
        {
            worldManifest = WorldManifest.Create(new WorldManifestDto
            {
                occupied = new[] { "5,7" },
                spawn_coordinate = new SpawnCoordinateData(5, 7),
                total = 1,
            });
            realmData.WorldManifest.Returns(worldManifest);
            appArgs.HasFlag(AppArgsFlags.POSITION).Returns(false);

            CreateOperation(new StartParcel(new Vector2Int(10, 20)))
                .ExecuteAsync(MakeParams(), cts.Token).GetAwaiter().GetResult();

            teleportController.Received(1)
                .TeleportToSceneSpawnPointAsync(new Vector2Int(5, 7), Arg.Any<AsyncLoadProcessReport>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public void UsesStartParcelWhenPositionArgIsSetEvenWithWorldManifest()
        {
            worldManifest = WorldManifest.Create(new WorldManifestDto
            {
                occupied = new[] { "10,20" },
                spawn_coordinate = new SpawnCoordinateData(5, 7),
                total = 1,
            });
            realmData.WorldManifest.Returns(worldManifest);
            appArgs.HasFlag(AppArgsFlags.POSITION).Returns(true);

            CreateOperation(new StartParcel(new Vector2Int(10, 20)))
                .ExecuteAsync(MakeParams(), cts.Token).GetAwaiter().GetResult();

            teleportController.Received(1)
                .TeleportToSceneSpawnPointAsync(new Vector2Int(10, 20), Arg.Any<AsyncLoadProcessReport>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public void UsesStartParcelWhenEditorPositionOverrideActiveEvenWithWorldManifest()
        {
            worldManifest = WorldManifest.Create(new WorldManifestDto
            {
                occupied = new[] { "10,20" },
                spawn_coordinate = new SpawnCoordinateData(5, 7),
                total = 1,
            });
            realmData.WorldManifest.Returns(worldManifest);
            appArgs.HasFlag(AppArgsFlags.POSITION).Returns(false);

            CreateOperation(new StartParcel(new Vector2Int(10, 20)), editorPositionOverrideActive: true)
                .ExecuteAsync(MakeParams(), cts.Token).GetAwaiter().GetResult();

            teleportController.Received(1)
                .TeleportToSceneSpawnPointAsync(new Vector2Int(10, 20), Arg.Any<AsyncLoadProcessReport>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public void UsesStartParcelWhenNonWorldRealm()
        {
            realmData.RealmType.Returns(new ReactiveProperty<RealmKind>(RealmKind.GenesisCity));
            appArgs.HasFlag(AppArgsFlags.POSITION).Returns(false);

            CreateOperation(new StartParcel(new Vector2Int(10, 20)))
                .ExecuteAsync(MakeParams(), cts.Token).GetAwaiter().GetResult();

            teleportController.Received(1)
                .TeleportToSceneSpawnPointAsync(new Vector2Int(10, 20), Arg.Any<AsyncLoadProcessReport>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public void StartParcelIsConsumedAfterTeleport()
        {
            realmData.RealmType.Returns(new ReactiveProperty<RealmKind>(RealmKind.GenesisCity));
            appArgs.HasFlag(AppArgsFlags.POSITION).Returns(false);

            var startParcel = new StartParcel(new Vector2Int(10, 20));
            CreateOperation(startParcel).ExecuteAsync(MakeParams(), cts.Token).GetAwaiter().GetResult();

            Assert.IsTrue(startParcel.IsConsumed());
        }

        private IStartupOperation.Params MakeParams()
        {
            Entity playerEntity = world.Create();

            var flowParams = new UserInAppInitializationFlowParameters(
                showAuthentication: false,
                showLoading: false,
                loadSource: IUserInAppInitializationFlow.LoadSource.StartUp,
                world: world,
                playerEntity: playerEntity);

            return new IStartupOperation.Params(AsyncLoadProcessReport.Create(cts.Token), flowParams);
        }

        private TeleportStartupOperation CreateOperation(StartParcel startParcel, bool editorPositionOverrideActive = false) =>
            new TeleportStartupOperation(loadingStatus, realmController, cameraEntityProxy,
                teleportController, new CameraSamplingData(), startParcel, appArgs, editorPositionOverrideActive);
    }
}
