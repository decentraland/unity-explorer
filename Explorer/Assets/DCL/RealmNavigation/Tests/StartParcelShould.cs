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
            AssignResult realmResult = startParcel.AssignRealm(URLDomain.FromString("https://realm.example.com/main"));
            AssignResult parcelResult = startParcel.Assign(new Vector2Int(1, 2));

            // Assert
            Assert.That(parcelResult, Is.EqualTo(AssignResult.Ok));
            Assert.That(realmResult, Is.EqualTo(AssignResult.Ok));
            Assert.That(startParcel.ConsumeByTeleportOperation(), Is.EqualTo(new Vector2Int(1, 2)));
        }

        [Test]
        public void ReportAJumpInRequestBeforeTheTeleport()
        {
            // Arrange
            var startParcel = new StartParcel(LAUNCH_PARCEL);
            int requests = 0;
            startParcel.JumpInRequestRaised += () => requests++;

            // Act
            startParcel.RequestJumpIn();

            // Assert
            Assert.That(startParcel.JumpInRequested, Is.True);
            Assert.That(requests, Is.EqualTo(1));
        }

        [Test]
        public void IgnoreAJumpInRequestOnceConsumed()
        {
            // Arrange
            var startParcel = new StartParcel(LAUNCH_PARCEL);
            int requests = 0;
            startParcel.JumpInRequestRaised += () => requests++;
            startParcel.ConsumeByTeleportOperation();

            // Act
            startParcel.RequestJumpIn();

            // Assert
            Assert.That(startParcel.JumpInRequested, Is.False);
            Assert.That(requests, Is.Zero);
        }

        [Test]
        public void ForgetTheJumpInRequestAndTheAssignedParcelOnReset()
        {
            // Arrange
            var startParcel = new StartParcel(LAUNCH_PARCEL);
            startParcel.Assign(new Vector2Int(1, 2));
            startParcel.RequestJumpIn();
            startParcel.ConsumeByTeleportOperation();

            // Act
            startParcel.Reset();

            // Assert
            Assert.That(startParcel.JumpInRequested, Is.False);
            Assert.That(startParcel.IsParcelAssigned, Is.False);
        }

        [Test]
        public void ForgetTheAppliedRealmOnReset()
        {
            // Arrange
            var startParcel = new StartParcel(LAUNCH_PARCEL);
            startParcel.MarkRealmApplied();
            startParcel.ConsumeByTeleportOperation();

            // Act
            startParcel.Reset();

            // Assert
            Assert.That(startParcel.IsRealmApplied, Is.False);
        }

        [Test]
        public void CarryTheSpawnPointOfTheAssignedRealm()
        {
            // Arrange
            var startParcel = new StartParcel(LAUNCH_PARCEL, LAUNCH_SPAWN_POINT);

            // Act
            startParcel.AssignRealm(URLDomain.FromString("https://worlds.example.com/myworld.dcl.eth"), "physics");

            // Assert
            Assert.That(startParcel.SpawnPointName, Is.EqualTo("physics"));
            Assert.That(startParcel.IsParcelAssigned, Is.False);
        }

        [Test]
        public void DropAnEarlierParcelWhenARealmIsAssigned()
        {
            // Arrange
            var startParcel = new StartParcel(LAUNCH_PARCEL);
            startParcel.Assign(new Vector2Int(10, 10));

            // Act
            startParcel.AssignRealm(URLDomain.FromString("https://worlds.example.com/myworld.dcl.eth"));

            // Assert
            Assert.That(startParcel.IsParcelAssigned, Is.False);
            Assert.That(startParcel.Peek(), Is.EqualTo(LAUNCH_PARCEL));
        }

        [Test]
        public void TakeAnotherRealmOnceTheAppliedOneIsCleared()
        {
            // Arrange
            var startParcel = new StartParcel(LAUNCH_PARCEL);
            startParcel.MarkRealmApplied();

            // Act
            startParcel.ClearRealmApplied();

            // Assert
            Assert.That(startParcel.IsRealmApplied, Is.False);
        }

        [Test]
        public void KeepTheAppliedRealmOnceConsumed()
        {
            // Arrange
            var startParcel = new StartParcel(LAUNCH_PARCEL);
            startParcel.MarkRealmApplied();
            startParcel.ConsumeByTeleportOperation();

            // Act
            startParcel.ClearRealmApplied();

            // Assert
            Assert.That(startParcel.IsRealmApplied, Is.True);
        }

        [Test]
        public void DropTheLaunchSpawnPointWhenARealmIsAssignedWithoutOne()
        {
            // Arrange
            var startParcel = new StartParcel(LAUNCH_PARCEL, LAUNCH_SPAWN_POINT);

            // Act
            startParcel.AssignRealm(URLDomain.FromString("https://worlds.example.com/myworld.dcl.eth"));

            // Assert
            Assert.That(startParcel.SpawnPointName, Is.Null);
        }
    }
}
