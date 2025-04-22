using Arch.Core;
using Arch.Core.Extensions;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using DCL.Ipfs;
using DCL.LOD;
using DCL.LOD.Components;
using DCL.Utilities.Extensions;
using ECS.LifeCycle.Components;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SceneRunner.Scene;
using System.Threading;
using UnityEngine;
using Utility;

namespace ECS.SceneLifeCycle.Tests
{
    public class ResolveSceneStateByIncreasingRadiusSystemShould : UnitySystemTestBase<ResolveSceneStateByIncreasingRadiusSystem>
    {
        private IRealmPartitionSettings realmPartitionSettings;
        private RealmComponent realmComponent;

        private SceneLoadingLimit sceneLoadingLimit;
        private int maximumAmountOfScenesThatCanLoad;
        private int maximumAmountOfLODThatCanLoad;
        private int maximumAmountOfLODReductedThatCanLoad;

        private int SDK7LODThreshold;
        private Entity playerEntity;



        [SetUp]
        public void SetUp()
        {
            playerEntity = world.Create(new CharacterTransform(new GameObject().transform));

            ILODSettingsAsset lodSettingsAsset = ScriptableObject.CreateInstance<LODSettingsAsset>();
            SDK7LODThreshold = 2;
            lodSettingsAsset.SDK7LodThreshold = SDK7LODThreshold;
            lodSettingsAsset.UnloadTolerance = 1;

            VisualSceneStateResolver visualSceneStateResolver = new VisualSceneStateResolver(lodSettingsAsset);

            RealmData realmData = new RealmData(new TestIpfsRealm());

            maximumAmountOfScenesThatCanLoad = 6;
            maximumAmountOfLODThatCanLoad = 5;
            maximumAmountOfLODReductedThatCanLoad = 8;

            sceneLoadingLimit = SceneLoadingLimit.CreateMax();
            sceneLoadingLimit.MaximumAmountOfScenesThatCanLoad = maximumAmountOfScenesThatCanLoad;
            sceneLoadingLimit.MaximumAmountOfLODsThatCanLoad = maximumAmountOfLODThatCanLoad;
            sceneLoadingLimit.MaximumAmountOfReductedLoDsThatCanLoad = maximumAmountOfLODReductedThatCanLoad;

            realmPartitionSettings = Substitute.For<IRealmPartitionSettings>();
            system = new ResolveSceneStateByIncreasingRadiusSystem(world, realmPartitionSettings, playerEntity, visualSceneStateResolver, realmData, sceneLoadingLimit);

            realmComponent = new RealmComponent(realmData);
            world.Create(realmComponent, new VolatileScenePointers());
        }

        [Test]
        [TestCase(0, "7")]
        [TestCase(20, "7")]
        [TestCase(20, "6")]
        public async Task LimitSceneLoadingByMemory(int sceneAmount, string runtimeVersion)
        {
            realmPartitionSettings.ScenesRequestBatchSize.Returns(30);
            realmPartitionSettings.MaxLoadingDistanceInParcels.Returns(3000);
            CreateScenes(realmPartitionSettings.ScenesRequestBatchSize, runtimeVersion, sceneAmount);

            system.Update(0f);

            // Wait for job to complete
            while (!system.sortingJobHandle.Value.IsCompleted)
                await Task.Yield();

            system.Update(0f);

            //If no scene were requested, or all of them were sdk6
            AssertResult(sceneAmount == 0 || runtimeVersion != "7" ? 0 : sceneLoadingLimit.MaximumAmountOfScenesThatCanLoad, sceneLoadingLimit.MaximumAmountOfLODsThatCanLoad + sceneLoadingLimit.MaximumAmountOfReductedLoDsThatCanLoad,
                sceneLoadingLimit.MaximumAmountOfLODsThatCanLoad, sceneLoadingLimit.MaximumAmountOfReductedLoDsThatCanLoad);
        }

        [Test]
        public async Task AllowOnlyOneSceneWhileTeleporting()
        {
            world.Add(playerEntity, new PlayerTeleportIntent(null, Vector3.zero.ToParcel(), Vector3.zero, CancellationToken.None));
            realmPartitionSettings.ScenesRequestBatchSize.Returns(30);
            realmPartitionSettings.MaxLoadingDistanceInParcels.Returns(3000);

            CreateScenes(realmPartitionSettings.ScenesRequestBatchSize, "7", 20);

            system.Update(0f);

            // Wait for job to complete
            while (!system.sortingJobHandle.Value.IsCompleted)
                await Task.Yield();

            system.Update(0f);

            AssertResult(1, 0, 0, 0);
        }

        [Test]
        public async Task LimitScenesLoading()
        {
            realmPartitionSettings.ScenesRequestBatchSize.Returns(2);
            realmPartitionSettings.MaxLoadingDistanceInParcels.Returns(3000);

            // Create 4
            for (var i = 0; i < 4; i++)
            {
                world.Create(SceneDefinitionComponentFactory.CreateFromDefinition(
                    new SceneEntityDefinition
                    {
                        metadata = new SceneMetadata
                        {
                            scene = new SceneMetadataScene
                                { DecodedParcels = new Vector2Int[] { new (0, 0), new (0, 1), new (1, 0), new (2, 0), new (2, 1), new (3, 0), new (3, 1) } },
                            runtimeVersion = "7"
                        },
                    },
                    new IpfsPath()), new PartitionComponent
                {
                    Bucket = (byte)i, RawSqrDistance = ParcelMathHelper.SQR_PARCEL_SIZE * i,
                });
            }

            system.Update(0f);

            // Wait for job to complete
            while (!system.sortingJobHandle.Value.IsCompleted)
                await Task.Yield();

            system.Update(0f);

            // Serve 2
            var entities = new List<Entity>();
            world.GetEntities(new QueryDescription().WithAll<GetSceneFacadeIntention>(), entities.AsSpan());

            Assert.That(entities.Count, Is.EqualTo(2));
            Assert.That(entities.Any(e => world.Get<IPartitionComponent>(e).Bucket == 0), Is.True);
            Assert.That(entities.Any(e => world.Get<IPartitionComponent>(e).Bucket == 1), Is.True);
        }

        [Test]
        public void StartUnloading()
        {
            realmPartitionSettings.UnloadingDistanceToleranceInParcels.Returns(1);
            realmPartitionSettings.MaxLoadingDistanceInParcels.Returns(1);

            for (byte i = 2; i <= 4; i++)
            {
                world.Create(SceneDefinitionComponentFactory.CreateFromDefinition(
                    new SceneEntityDefinition
                    {
                        metadata = new SceneMetadata
                        {
                            scene = new SceneMetadataScene
                            {
                                DecodedParcels = new Vector2Int[] { new (0, 0), new (0, 1), new (1, 0), new (2, 0), new (2, 1), new (3, 0), new (3, 1) },
                            },
                        },
                    },
                    new IpfsPath()), new PartitionComponent
                {
                    Bucket = i, RawSqrDistance = (ParcelMathHelper.PARCEL_SIZE * i * ParcelMathHelper.PARCEL_SIZE * i) - 1f, OutOfRange = i < 4,
                }, Substitute.For<ISceneFacade>(),
                    SceneLoadingState.CreateBuiltScene());
            }

            system.Update(0f);

            Assert.That(world.CountEntities(new QueryDescription().WithAll<DeleteEntityIntention>()), Is.EqualTo(2));
        }

        private void AssertResult(int sceneResultExpected, int lodResultExpected, int lodHighQualityResultExpected, int lodLowQualityResultExpected)
        {
            var sceneEntities = new List<Entity>();
            var lodEntities = new List<Entity>();

            world.GetEntities(new QueryDescription().WithAll<GetSceneFacadeIntention>(), sceneEntities.AsSpan());
            world.GetEntities(new QueryDescription().WithAll<SceneLODInfo>(), lodEntities.AsSpan());

            var qualityReductedLODCount = 0;
            var qualityHighLODCount = 0;

            foreach (Entity lodEntity in lodEntities)
            {
                SceneLoadingState sceneLoadingState = world.Get<SceneLoadingState>(lodEntity);

                if (sceneLoadingState.FullQuality)
                    qualityHighLODCount++;
                else
                    qualityReductedLODCount++;
            }

            Assert.That(sceneEntities.Count, Is.EqualTo(sceneResultExpected));
            Assert.That(lodEntities.Count, Is.EqualTo(lodResultExpected));
            Assert.That(qualityHighLODCount, Is.EqualTo(lodHighQualityResultExpected));
            Assert.That(qualityReductedLODCount, Is.EqualTo(lodLowQualityResultExpected));
        }

        private void CreateScenes(int sceneBatch, string runtime, int sceneAmount)
        {
            // Create 30 scene candidate.
            for (var i = 0; i < sceneBatch; i++)
            {
                world.Create(SceneDefinitionComponentFactory.CreateFromDefinition(
                    new SceneEntityDefinition
                    {
                        metadata = new SceneMetadata
                        {
                            scene = new SceneMetadataScene
                                { DecodedParcels = new Vector2Int[] { new (0, 0), new (0, 1), new (1, 0), new (2, 0), new (2, 1), new (3, 0), new (3, 1) } },
                            runtimeVersion = runtime,
                        },
                    },
                    new IpfsPath()), new PartitionComponent
                {
                    Bucket = (byte)(i < sceneAmount ? 0 : SDK7LODThreshold + 1), RawSqrDistance = ParcelMathHelper.SQR_PARCEL_SIZE * i,
                });
            }
        }
    }
}
