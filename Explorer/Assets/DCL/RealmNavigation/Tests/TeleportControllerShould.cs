using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using DCL.Ipfs;
using DCL.Utilities;
using ECS;
using ECS.SceneLifeCycle.Realm;
using ECS.SceneLifeCycle.Reporting;
using ECS.SceneLifeCycle.SingleScene;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DCL.RealmNavigation.Tests
{
    public class TeleportControllerShould
    {
        private World world = null!;
        private Entity playerEntity;
        private TeleportController controller = null!;
        private IRetrieveScene retrieveScene = null!;
        private ILandscape landscape = null!;
        private SingleSceneMode singleSceneMode = null!;
        private IRealmData realmData = null!;

        [SetUp]
        public void SetUp()
        {
            world = World.Create();
            playerEntity = world.Create(new PlayerComponent());

            retrieveScene = Substitute.For<IRetrieveScene>();
            landscape = Substitute.For<ILandscape>();
            singleSceneMode = new SingleSceneMode();
            realmData = new IRealmData.Fake();

            controller = new TeleportController(Substitute.For<ISceneReadinessReportQueue>(), singleSceneMode, realmData)
            {
                World = world,
                SceneProviderStrategy = retrieveScene,
                Landscape = landscape,
            };
        }

        [TearDown]
        public void TearDown() =>
            world.Dispose();

        // Regression test for #9546: the Discover -> Events page jump-in ("BBQ Sauce Recipe" @ -148,141)
        // targets coordinates that ARE the scene's own base parcel, yet EventCardActionsController still
        // passes landOnParcel: true. That flag must not survive when there is no sub-parcel to single out,
        // otherwise TeleportPositionCalculationSystem takes the raw parcel-center branch instead of the
        // authored spawn point ("SpawnArea1") that every other teleport entry point (chat/map) resolves to.
        [Test]
        public async Task ClearLandOnParcelWhenTargetParcelIsTheSceneBase()
        {
            var baseParcel = new Vector2Int(-148, 141);

            SceneEntityDefinition sceneDef = BuildSceneDefWithDefaultSpawnPoint(baseParcel);

            retrieveScene.ByParcelAsync(baseParcel, Arg.Any<CancellationToken>())
                         .Returns(UniTask.FromResult<SceneEntityDefinition?>(sceneDef));

            AsyncLoadProcessReport loadReport = AsyncLoadProcessReport.Create(CancellationToken.None);

            await controller.TeleportToSceneSpawnPointAsync(baseParcel, loadReport, CancellationToken.None, landOnParcel: true);

            Assert.That(world.Has<PlayerTeleportIntent>(playerEntity), Is.True, "A PlayerTeleportIntent should have been queued on the player entity");

            var intent = world.Get<PlayerTeleportIntent>(playerEntity);

            // Unpatched: LandOnParcel stays true, so the position system takes the parcel-center path.
            // Patched: base-parcel targets have no sub-parcel to single out, so the flag is cleared and
            // spawn-point resolution (incl. the spawn's cameraTarget) runs instead - matching map/chat.
            Assert.That(intent.LandOnParcel, Is.False,
                "landOnParcel must be cleared when the requested parcel equals the scene's base parcel, so spawn-point resolution is used instead of the raw parcel center");
        }

        [Test]
        public async Task GenerateTheDestinationSceneTerrainWhileSingleSceneModeIsActive()
        {
            singleSceneMode.Init(true);

            var baseParcel = new Vector2Int(-148, 141);
            SceneEntityDefinition sceneDef = BuildSceneDefWithDefaultSpawnPoint(baseParcel);

            retrieveScene.ByParcelAsync(baseParcel, Arg.Any<CancellationToken>())
                         .Returns(UniTask.FromResult<SceneEntityDefinition?>(sceneDef));

            await controller.TeleportToSceneSpawnPointAsync(baseParcel, AsyncLoadProcessReport.Create(CancellationToken.None), CancellationToken.None);

            landscape.Received(1).GenerateSingleSceneTerrain(sceneDef.metadata.scene.DecodedParcels);
        }

        [Test]
        public async Task NotGenerateTerrainWhenSingleSceneModeIsInactive()
        {
            var baseParcel = new Vector2Int(-148, 141);
            SceneEntityDefinition sceneDef = BuildSceneDefWithDefaultSpawnPoint(baseParcel);

            retrieveScene.ByParcelAsync(baseParcel, Arg.Any<CancellationToken>())
                         .Returns(UniTask.FromResult<SceneEntityDefinition?>(sceneDef));

            await controller.TeleportToSceneSpawnPointAsync(baseParcel, AsyncLoadProcessReport.Create(CancellationToken.None), CancellationToken.None);

            landscape.DidNotReceiveWithAnyArgs().GenerateSingleSceneTerrain(default!);
        }

        [Test]
        public async Task NotGenerateTerrainOutsideGenesisWhileSingleSceneModeIsActive()
        {
            singleSceneMode.Init(true);

            var worldRealm = Substitute.For<IRealmData>();
            worldRealm.RealmType.Returns(new ReactiveProperty<RealmKind>(RealmKind.World));

            var worldController = new TeleportController(Substitute.For<ISceneReadinessReportQueue>(), singleSceneMode, worldRealm)
            {
                World = world,
                SceneProviderStrategy = retrieveScene,
                Landscape = landscape,
            };

            var baseParcel = new Vector2Int(-148, 141);
            SceneEntityDefinition sceneDef = BuildSceneDefWithDefaultSpawnPoint(baseParcel);

            retrieveScene.ByParcelAsync(baseParcel, Arg.Any<CancellationToken>())
                         .Returns(UniTask.FromResult<SceneEntityDefinition?>(sceneDef));

            await worldController.TeleportToSceneSpawnPointAsync(baseParcel, AsyncLoadProcessReport.Create(CancellationToken.None), CancellationToken.None);

            landscape.DidNotReceiveWithAnyArgs().GenerateSingleSceneTerrain(default!);
        }

        private static SceneEntityDefinition BuildSceneDefWithDefaultSpawnPoint(Vector2Int baseParcel)
        {
            var sceneSection = new SceneMetadataScene
            {
                DecodedBase = baseParcel,
                DecodedParcels = new[] { baseParcel },
            };

            var spawnPoint = new SceneMetadata.SpawnPoint
            {
                name = "SpawnArea1",
                @default = true,
                position = new SceneMetadata.SpawnPoint.Position
                {
                    x = new SceneMetadata.SpawnPoint.Coordinate { MultiValue = new[] { 0f, 3f } },
                    y = new SceneMetadata.SpawnPoint.Coordinate { SingleValue = 0f },
                    z = new SceneMetadata.SpawnPoint.Coordinate { MultiValue = new[] { 0f, 3f } },
                },
                cameraTarget = new SceneMetadata.SpawnPoint.Position
                {
                    x = new SceneMetadata.SpawnPoint.Coordinate { SingleValue = 8f },
                    y = new SceneMetadata.SpawnPoint.Coordinate { SingleValue = 1f },
                    z = new SceneMetadata.SpawnPoint.Coordinate { SingleValue = 8f },
                },
            };

            var metadata = new SceneMetadata
            {
                scene = sceneSection,
                spawnPoints = new List<SceneMetadata.SpawnPoint> { spawnPoint },
            };

            return new SceneEntityDefinition("grill-master-week-2", metadata);
        }
    }
}
