using Arch.Core;
using ECS.Prioritization;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using Utility;

namespace ECS.SceneLifeCycle.Tests
{
    public class CreateEmptyPointersInFixedRealmSystemShould : UnitySystemTestBase<CreateEmptyPointersInFixedRealmSystem>
    {
        private static readonly int2[] EXPECTED_CELLS_N1 =
        {
            new (0, 0),
            new (1, 1), new (1, 0), new (1, -1),
            new (0, -1), new (-1, -1),
            new (-1, -0), new (-1, 1), new (0, 1),
        };

        private ParcelMathJobifiedHelper mathJobifiedHelper;
        private IRealmPartitionSettings realmPartitionSettings;

        [SetUp]
        public void SetUp()
        {
            system = new CreateEmptyPointersInFixedRealmSystem(world,
                mathJobifiedHelper = new ParcelMathJobifiedHelper(),
                realmPartitionSettings = Substitute.For<IRealmPartitionSettings>());

            realmPartitionSettings.ScenesDefinitionsRequestBatchSize.Returns(int.MaxValue);
        }

        [TearDown]
        public void TearDown()
        {
            mathJobifiedHelper.Dispose();
        }

        [Test]
        public void CreatePointersForMissingScenes()
        {
            using var processed = new NativeHashSet<int2>(2, AllocatorManager.Persistent);
            processed.Add(new int2(1, -1));
            processed.Add(new int2(0, 1));

            using var processedCopy = processed.ToNativeArray(AllocatorManager.Persistent);

            mathJobifiedHelper.StartParcelsRingSplit(int2.zero, 1, processed);

            Entity e = world.Create(new RealmComponent(), new FixedScenePointers { AllPromisesResolved = true }, new ProcessedScenePointers { Value = processed });
            system.Update(0);

            var result = new List<int2>();

            world.Query(new QueryDescription().WithAll<SceneDefinitionComponent>(), (ref SceneDefinitionComponent scene) => result.AddRange(scene.Parcels.Select(p => p.ToInt2())));

            Assert.That(result, Is.EquivalentTo(EXPECTED_CELLS_N1.Except(processedCopy)));
        }
    }
}
