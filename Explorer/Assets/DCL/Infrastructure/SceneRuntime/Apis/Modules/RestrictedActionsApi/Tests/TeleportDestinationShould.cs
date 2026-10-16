using NUnit.Framework;
using UnityEngine;

namespace DCL.SceneRuntime.Apis.RestrictedActionsApi.Tests
{
    public class TeleportDestinationShould
    {
        private const string TEST_REALM = "TestRealm";
        private static readonly Vector2Int TEST_PARCEL = new (10, 20);

        [Test]
        public void CreateParcelWhenOnlyCoordinatesArePresent()
        {
            // Act
            bool created = TeleportDestination.TryCreate(TEST_PARCEL, null, out TeleportDestination destination);

            // Assert
            Assert.IsTrue(created);
            Assert.IsTrue(destination.IsParcel(out Vector2Int parcel));
            Assert.AreEqual(TEST_PARCEL, parcel);
        }

        [Test]
        public void CreateRealmWithParcelWhenBothArePresent()
        {
            // Act
            bool created = TeleportDestination.TryCreate(TEST_PARCEL, TEST_REALM, out TeleportDestination destination);

            // Assert
            Assert.IsTrue(created);
            Assert.IsTrue(destination.IsRealm(out RealmDestination realm));
            Assert.AreEqual(TEST_REALM, realm.Realm);
            Assert.AreEqual(TEST_PARCEL, realm.Parcel);
        }

        [Test]
        public void CreateRealmWithoutParcelWhenOnlyRealmIsPresent()
        {
            // Act
            bool created = TeleportDestination.TryCreate(null, TEST_REALM, out TeleportDestination destination);

            // Assert
            Assert.IsTrue(created);
            Assert.IsTrue(destination.IsRealm(out RealmDestination realm));
            Assert.AreEqual(TEST_REALM, realm.Realm);
            Assert.IsNull(realm.Parcel);
        }

        [Test]
        public void TreatEmptyRealmAsAbsent()
        {
            // Act
            bool created = TeleportDestination.TryCreate(TEST_PARCEL, string.Empty, out TeleportDestination destination);

            // Assert
            Assert.IsTrue(created);
            Assert.IsTrue(destination.IsParcel(out _));
        }

        [TestCase(null)]
        [TestCase("")]
        public void RejectWhenNeitherCoordinatesNorRealmArePresent(string? realm)
        {
            // Act
            bool created = TeleportDestination.TryCreate(null, realm, out _);

            // Assert
            Assert.IsFalse(created);
        }
    }
}
