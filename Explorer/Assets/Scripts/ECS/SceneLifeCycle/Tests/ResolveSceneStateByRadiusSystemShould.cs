using Arch.Core;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using ECS.TestSuite;
using Ipfs;
using NUnit.Framework;
using Realm;
using SceneRunner.Scene;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace ECS.SceneLifeCycle.Tests
{
    public class ResolveSceneStateByRadiusSystemShould : UnitySystemTestBase<ResolveSceneStateByRadiusSystem>
    {
        private RealmComponent realmComponent;

        [SetUp]
        public void SetUp()
        {
            system = new ResolveSceneStateByRadiusSystem(world);
            realmComponent = new RealmComponent(new RealmData(new TestIpfsRealm()));
        }

        [Test]
        public void CreateStartPromisesIfInRange()
        {
            var parcels = new int2[] { new (0, 0), new (0, 1), new (1, 0), new (1, 1) };

            var parcelsInRange = new ParcelsInRange(new HashSet<int2>(parcels), 2);

            world.Create(parcelsInRange, realmComponent);

            // wider range
            var sceneParcels = new Vector2Int[] { new (0, 0), new (0, 1), new (1, 0), new (2, 0), new (2, 1), new (3, 0), new (3, 1) };

            Entity scene = world.Create(new SceneDefinitionComponent(new IpfsTypes.SceneEntityDefinition(),
                sceneParcels, new IpfsTypes.IpfsPath()), PartitionComponent.TOP_PRIORITY);

            system.Update(0f);

            Assert.That(world.Has<AssetPromise<ISceneFacade, GetSceneFacadeIntention>>(scene), Is.True);
        }

        [Test]
        public void NotCreateStartPromisesIfOutOfRange()
        {
            var parcelsInRange = new ParcelsInRange(new HashSet<int2>(new int2[] { new (5, 5), new (4, 4), new (3, 3), new (2, 2) }), 2);

            world.Create(parcelsInRange, realmComponent);

            // no match
            var sceneParcels = new Vector2Int[] { new (0, 0), new (0, 1) };

            Entity scene = world.Create(new SceneDefinitionComponent(new IpfsTypes.SceneEntityDefinition(), sceneParcels, new IpfsTypes.IpfsPath()),
                PartitionComponent.TOP_PRIORITY);

            system.Update(0f);

            Assert.That(world.Has<AssetPromise<ISceneFacade, GetSceneFacadeIntention>>(scene), Is.False);
        }

        [Test]
        public void AddDestroyIntentionIfOutOfRange()
        {
            var parcelsInRange = new ParcelsInRange(new HashSet<int2>(new int2[] { new (5, 5), new (4, 4), new (3, 3), new (2, 2) }), 2);
            world.Create(parcelsInRange, realmComponent);

            // no match
            var sceneParcels = new Vector2Int[] { new (0, 0), new (0, 1) };
            Entity scene = world.Create(new SceneDefinitionComponent(new IpfsTypes.SceneEntityDefinition(), sceneParcels, new IpfsTypes.IpfsPath()), PartitionComponent.TOP_PRIORITY);

            system.Update(0);

            Assert.That(world.Has<DeleteEntityIntention>(scene), Is.True);
        }

        [Test]
        public void NotAddDestroyIntentionIfInRange()
        {
            var parcelsInRange = new ParcelsInRange(new HashSet<int2>(new int2[] { new (5, 5), new (4, 4), new (3, 3), new (2, 2) }), 2);
            world.Create(parcelsInRange, realmComponent);

            // match
            var sceneParcels = new Vector2Int[] { new (5, 5), new (0, 1) };
            Entity scene = world.Create(new SceneDefinitionComponent(new IpfsTypes.SceneEntityDefinition(), sceneParcels, new IpfsTypes.IpfsPath()), PartitionComponent.TOP_PRIORITY);

            system.Update(0);

            Assert.That(world.Has<DeleteEntityIntention>(scene), Is.False);
        }
    }
}
