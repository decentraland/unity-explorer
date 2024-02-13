using Arch.Core;
using DCL.Ipfs;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.SceneLifeCycle.Systems;
using ECS.StreamableLoading.Common;
using ECS.TestSuite;
using NUnit.Framework;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;

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
            var array = new int2[]
            {
                new (10, 10),
                new (-1, 2),
                new (3, 4),
            };

            var parcelsInRange = new ParcelsInRange(new HashSet<int2>(array), 2);

            var volatilePointers = new VolatileScenePointers(new List<SceneEntityDefinition>(), new List<int2>());

            var realm = new RealmComponent(new RealmData(new TestIpfsRealm()));

            Entity e = world.Create(parcelsInRange, volatilePointers, realm, new ProcessesScenePointers { Value = new NativeHashSet<int2>(100, AllocatorManager.Persistent) });

            system.Update(0);

            volatilePointers = world.Get<VolatileScenePointers>(e);

            Assert.That(volatilePointers.ActivePromise.HasValue, Is.True);
            AssetPromise<SceneDefinitions, GetSceneDefinitionList> promise = volatilePointers.ActivePromise.Value;
            Assert.That(promise.LoadingIntention.Pointers, Is.EquivalentTo(array));
        }

        [Test]
        public void NotCreatePromiseIfParcelsAreProcessed()
        {
            var array = new int2[]
            {
                new (10, 10),
                new (-1, 2),
                new (3, 4),
            };

            var parcelsInRange = new ParcelsInRange(new HashSet<int2>(array), 2);

            var processedParcels = new NativeHashSet<int2>(100, AllocatorManager.Persistent);

            var volatilePointers = new VolatileScenePointers(new List<SceneEntityDefinition>(),
                new List<int2>());

            foreach (int2 vector2Int in array)
                processedParcels.Add(vector2Int);

            var realm = new RealmComponent(new RealmData(new TestIpfsRealm()));

            Entity e = world.Create(parcelsInRange, volatilePointers, realm, new ProcessesScenePointers { Value = processedParcels });

            system.Update(0);

            volatilePointers = world.Get<VolatileScenePointers>(e);
            Assert.That(volatilePointers.ActivePromise.HasValue, Is.False);
        }
    }
}
