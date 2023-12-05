using Arch.Core;
using Arch.Core.Extensions;
using ECS.LifeCycle.Components;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.TestSuite;
using Ipfs;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
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
            realmPartitionSettings = Substitute.For<IRealmPartitionSettings>();
            system = new ResolveSceneStateByIncreasingRadiusSystem(world, realmPartitionSettings);

            realmComponent = new RealmComponent(new RealmData(new TestIpfsRealm()));
        }

        [Test]
        public void LimitScenesLoading()
        {
            realmPartitionSettings.ScenesRequestBatchSize.Returns(2);
            realmPartitionSettings.MaxLoadingDistanceInParcels.Returns(int.MaxValue);

            world.Create(realmComponent, new VolatileScenePointers());

            // Create 4
            for (var i = 0; i < 4; i++)
            {
                world.Create(new SceneDefinitionComponent(
                    new IpfsTypes.SceneEntityDefinition
                    {
                        metadata = new IpfsTypes.SceneMetadata
                        {
                            scene = new IpfsTypes.SceneMetadataScene
                                { DecodedParcels = new Vector2Int[] { new (0, 0), new (0, 1), new (1, 0), new (2, 0), new (2, 1), new (3, 0), new (3, 1) } },
                        },
                    },
                    new IpfsTypes.IpfsPath()), new PartitionComponent { Bucket = (byte)i, RawSqrDistance = ParcelMathHelper.SQR_PARCEL_SIZE * i });
            }

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
            realmPartitionSettings.UnloadBucket.Returns(3);

            for (byte i = 2; i <= 4; i++)
            {
                world.Create(new SceneDefinitionComponent(
                    new IpfsTypes.SceneEntityDefinition
                    {
                        metadata = new IpfsTypes.SceneMetadata
                        {
                            scene = new IpfsTypes.SceneMetadataScene
                            {
                                DecodedParcels = new Vector2Int[] { new (0, 0), new (0, 1), new (1, 0), new (2, 0), new (2, 1), new (3, 0), new (3, 1) },
                            },
                        },
                    },
                    new IpfsTypes.IpfsPath()), new PartitionComponent { Bucket = i });
            }

            system.Update(0f);

            Assert.That(world.CountEntities(new QueryDescription().WithAll<DeleteEntityIntention>()), Is.EqualTo(2));
        }
    }
}
