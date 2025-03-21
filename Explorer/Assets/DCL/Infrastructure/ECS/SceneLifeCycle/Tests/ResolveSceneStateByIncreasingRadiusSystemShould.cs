using Arch.Core;
using Arch.Core.Extensions;
using DCL.Character.Components;
using DCL.Ipfs;
using DCL.LOD;
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
using UnityEngine;
using Utility;

namespace ECS.SceneLifeCycle.Tests
{
    public class ResolveSceneStateByIncreasingRadiusSystemShould : UnitySystemTestBase<ResolveSceneStateByIncreasingRadiusSystem>
    {
        private IRealmPartitionSettings realmPartitionSettings;
        private RealmComponent realmComponent;

        [SetUp]
        public void SetUp()
        {
            Entity playerEntity = world.Create(new CharacterTransform(new GameObject().transform));

            ILODSettingsAsset lodSettingsAsset = ScriptableObject.CreateInstance<LODSettingsAsset>();
            lodSettingsAsset.SDK7LodThreshold = 2;
            lodSettingsAsset.UnloadTolerance = 1;

            VisualSceneStateResolver visualSceneStateResolver = new VisualSceneStateResolver(lodSettingsAsset);

            RealmData realmData = new RealmData(new TestIpfsRealm());

            SceneLoadingLimit sceneLoadingLimit = SceneLoadingLimit.CreateMax();

            realmPartitionSettings = Substitute.For<IRealmPartitionSettings>();
            system = new ResolveSceneStateByIncreasingRadiusSystem(world, realmPartitionSettings, playerEntity, visualSceneStateResolver, realmData, sceneLoadingLimit);

            realmComponent = new RealmComponent(realmData);
            world.Create(realmComponent, new VolatileScenePointers());
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
            world.GetEntities(new QueryDescription().WithAll<GetSceneFacadeIntention>(), entities);

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
    }
}
