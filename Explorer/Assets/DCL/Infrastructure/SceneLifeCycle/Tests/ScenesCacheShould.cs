using ECS.SceneLifeCycle;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using UnityEngine;

namespace DCL.SceneLifeCycle.Tests
{
    [TestFixture]
    public class ScenesCacheShould
    {
        // Genesis-City parcel from the reported crash key (-4, -4).
        private static readonly Vector2Int PARCEL = new (-4, -4);

        private ScenesCache cache;
        private ISceneFacade oldFacade;
        private ISceneFacade newFacade;
        private Vector2Int[] parcels;

        [SetUp]
        public void SetUp()
        {
            cache = new ScenesCache();
            oldFacade = Substitute.For<ISceneFacade>();
            newFacade = Substitute.For<ISceneFacade>();
            parcels = new[] { PARCEL };
        }

        [Test]
        public void UpsertDuplicateParcelWithoutThrowingAndKeepNewestFacade()
        {
            cache.Add(oldFacade, parcels);

            Assert.DoesNotThrow(() => cache.Add(newFacade, parcels));

            Assert.That(cache.TryGetByParcel(PARCEL, out ISceneFacade resolved), Is.True);
            Assert.That(resolved, Is.SameAs(newFacade));
        }

        [Test]
        public void RemoveSceneFacadeOnlyEvictsTheParcelWhenFacadeMatches()
        {
            cache.Add(oldFacade, parcels);
            cache.Add(newFacade, parcels); // upsert: newFacade now owns PARCEL

            cache.RemoveSceneFacade(oldFacade, parcels);

            Assert.That(cache.TryGetByParcel(PARCEL, out ISceneFacade resolved), Is.True);
            Assert.That(resolved, Is.SameAs(newFacade));
            Assert.That(cache.Scenes, Contains.Item(newFacade));
            Assert.That(cache.Scenes, Has.No.Member(oldFacade));
        }
    }
}
