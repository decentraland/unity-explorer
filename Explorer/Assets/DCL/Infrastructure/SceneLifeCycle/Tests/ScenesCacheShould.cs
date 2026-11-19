using ECS.SceneLifeCycle;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;

namespace DCL.SceneLifeCycle.Tests
{
    [TestFixture]
    public class ScenesCacheShould
    {
        private ScenesCache scenesCache = null!;

        [SetUp]
        public void SetUp()
        {
            scenesCache = new ScenesCache();
        }

        [Test]
        public void AddSceneListingTheSameParcelTwice()
        {
            // Arrange
            ISceneFacade scene = Substitute.For<ISceneFacade>();
            var parcel = new Vector2Int(1, 2);

            // Act
            scenesCache.Add(scene, new[] { parcel, parcel });

            // Assert
            Assert.That(scenesCache.TryGetByParcel(parcel, out ISceneFacade cached), Is.True);
            Assert.That(cached, Is.SameAs(scene));
            Assert.That(scenesCache.Scenes, Has.Member(scene));
        }

        [Test]
        public void KeepOwnerOfOverlappingParcelWhenOverlappingSceneIsRemoved()
        {
            // Arrange
            ISceneFacade owner = Substitute.For<ISceneFacade>();
            ISceneFacade overlapping = Substitute.For<ISceneFacade>();
            var sharedParcel = new Vector2Int(0, 0);
            Vector2Int[] overlappingParcels = { sharedParcel, new (0, 1) };

            scenesCache.Add(owner, new[] { sharedParcel, new Vector2Int(1, 0) });

            LogAssert.Expect(LogType.Error, new Regex("already owned by another scene"));

            // Act
            scenesCache.Add(overlapping, overlappingParcels);

            // Assert
            Assert.That(scenesCache.TryGetByParcel(sharedParcel, out ISceneFacade cachedAfterAdd), Is.True);
            Assert.That(cachedAfterAdd, Is.SameAs(owner));

            // Act
            scenesCache.RemoveSceneFacade(overlapping, overlappingParcels);

            // Assert
            Assert.That(scenesCache.TryGetByParcel(sharedParcel, out ISceneFacade cachedAfterRemove), Is.True);
            Assert.That(cachedAfterRemove, Is.SameAs(owner));
            Assert.That(scenesCache.TryGetByParcel(new Vector2Int(0, 1), out _), Is.False);
            Assert.That(scenesCache.Scenes, Has.Member(owner));
            Assert.That(scenesCache.Scenes, Has.No.Member(overlapping));
        }
    }
}
