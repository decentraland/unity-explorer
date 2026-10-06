using DCL.Profiles;
using NSubstitute;
using NUnit.Framework;

namespace DCL.Multiplayer.Connections.Pulse.Tests
{
    [TestFixture]
    public class PulseProfilePropagationBusShould
    {
        private const string ALICE = "0x0000000000000000000000000000000000000001";
        private const string BOB = "0x0000000000000000000000000000000000000002";

        private IPulseMultiplayerService service = null!;
        private PulseProfilePropagationBus bus = null!;

        [SetUp]
        public void SetUp()
        {
            service = Substitute.For<IPulseMultiplayerService>();
            service.IsAuthenticated.Returns(true);
            bus = new PulseProfilePropagationBus(service);
        }

        [Test]
        public void AnnounceACopyAtAnAnnouncedVersionOnce()
        {
            // Arrange
            Profile announced = NewProfile(ALICE, 3);
            Profile copy = NewProfile(ALICE, 3);

            // Act
            bus.PropagateIfNewVersion(announced);
            bus.PropagateIfNewVersion(copy);

            // Assert
            service.ReceivedWithAnyArgs(1).Send(default);
        }

        [Test]
        public void AnnounceANewVersion()
        {
            // Act
            bus.PropagateIfNewVersion(NewProfile(ALICE, 3));
            bus.PropagateIfNewVersion(NewProfile(ALICE, 4));

            // Assert
            service.ReceivedWithAnyArgs(2).Send(default);
        }

        [Test]
        public void AnnounceAnotherUserAtTheSameVersion()
        {
            // Act
            bus.PropagateIfNewVersion(NewProfile(ALICE, 3));
            bus.PropagateIfNewVersion(NewProfile(BOB, 3));

            // Assert
            service.ReceivedWithAnyArgs(2).Send(default);
        }

        [Test]
        public void NeitherSendNorRecordBeforeAuthentication()
        {
            // Arrange
            Profile profile = NewProfile(ALICE, 3);
            service.IsAuthenticated.Returns(false);

            // Act
            bus.PropagateIfNewVersion(profile);
            service.IsAuthenticated.Returns(true);
            bus.PropagateIfNewVersion(profile);

            // Assert
            service.ReceivedWithAnyArgs(1).Send(default);
        }

        private static Profile NewProfile(string wallet, int version)
        {
            Profile profile = Profile.NewRandomProfile(wallet);
            profile.Version = version;
            return profile;
        }
    }
}
