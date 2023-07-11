using Arch.Core;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.SceneLifeCycle.Systems;
using ECS.StreamableLoading.Common;
using ECS.TestSuite;
using Ipfs;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace ECS.SceneLifeCycle.Tests
{
    public class LoadPointersByRadiusSystemShould : UnitySystemTestBase<LoadPointersByRadiusSystem>
    {
        [SetUp]
        public void SetUp()
        {
            system = new LoadPointersByRadiusSystem(world);
        }

        [Test]
        public void StartLoading()
        {
            var array = new Vector2Int[]
            {
                new (10, 10),
                new (-1, 2),
                new (3, 4),
            };

            var parcelsInRange = new ParcelsInRange(new HashSet<Vector2Int>(array), 2);

            var volatilePointers = new VolatileScenePointers(new List<IpfsTypes.SceneEntityDefinition>(),
                new HashSet<Vector2Int>(),
                new List<Vector2Int>());

            var realm = new RealmComponent(new TestIpfsRealm());

            Entity e = world.Create(parcelsInRange, volatilePointers, realm);

            system.Update(0);

            volatilePointers = world.Get<VolatileScenePointers>(e);

            Assert.That(volatilePointers.ActivePromise.HasValue, Is.True);
            AssetPromise<SceneDefinitions, GetSceneDefinitionList> promise = volatilePointers.ActivePromise.Value;
            Assert.That(promise.LoadingIntention.Pointers, Is.EquivalentTo(array));
        }

        [Test]
        public void NotCreatePromiseIfParcelsAreProcessed()
        {
            var array = new Vector2Int[]
            {
                new (10, 10),
                new (-1, 2),
                new (3, 4),
            };

            var parcelsInRange = new ParcelsInRange(new HashSet<Vector2Int>(array), 2);

            var volatilePointers = new VolatileScenePointers(new List<IpfsTypes.SceneEntityDefinition>(),
                new HashSet<Vector2Int>(),
                new List<Vector2Int>());

            foreach (Vector2Int vector2Int in array)
                volatilePointers.ProcessedParcels.Add(vector2Int);

            var realm = new RealmComponent(new TestIpfsRealm());

            Entity e = world.Create(parcelsInRange, volatilePointers, realm);

            system.Update(0);

            volatilePointers = world.Get<VolatileScenePointers>(e);
            Assert.That(volatilePointers.ActivePromise.HasValue, Is.False);
        }
    }
}
