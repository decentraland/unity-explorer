using CommunicationData.URLHelpers;
using NUnit.Framework;
using UnityEngine;

namespace DCL.RealmNavigation.Tests
{
    public class StartParcelShould
    {
        private const string LAUNCH_SPAWN_POINT = "entrance";

        private static readonly Vector2Int LAUNCH_PARCEL = new (10, 20);

        [Test]
        public void RestoreTheLaunchDestinationOnReset()
        {
            // Arrange: a session picked another destination and landed there
            var startParcel = new StartParcel(LAUNCH_PARCEL, LAUNCH_SPAWN_POINT, StartParcelSource.LaunchArgument);
            startParcel.Assign(new Vector2Int(-3, 7));
            startParcel.AssignRealm(URLDomain.FromString("https://worlds.example.com/myworld.dcl.eth"));
            startParcel.ConsumeByTeleportOperation();

            // Act
            startParcel.Reset();

            // Assert
            Assert.That(startParcel.IsConsumed(), Is.False);
            Assert.That(startParcel.Peek(), Is.EqualTo(LAUNCH_PARCEL));
            Assert.That(startParcel.SpawnPointName, Is.EqualTo(LAUNCH_SPAWN_POINT));
            Assert.That(startParcel.Realm, Is.Null);
            Assert.That(startParcel.Source, Is.EqualTo(StartParcelSource.LaunchArgument));
        }

        [Test]
        public void AcceptANewDestinationAfterReset()
        {
            // Arrange
            var startParcel = new StartParcel(LAUNCH_PARCEL);
            startParcel.ConsumeByTeleportOperation();
            startParcel.Reset();

            // Act
            AssignResult parcelResult = startParcel.Assign(new Vector2Int(1, 2));
            AssignResult realmResult = startParcel.AssignRealm(URLDomain.FromString("https://realm.example.com/main"));

            // Assert
            Assert.That(parcelResult, Is.EqualTo(AssignResult.Ok));
            Assert.That(realmResult, Is.EqualTo(AssignResult.Ok));
            Assert.That(startParcel.ConsumeByTeleportOperation(), Is.EqualTo(new Vector2Int(1, 2)));
        }
    }
}
