using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Connections.Systems.RoomIndicator;
using NUnit.Framework;

namespace DCL.Tests.Editor
{
    public class RoomIndicatorLabelShould
    {
        [Test]
        public void ReportNoneWhenNothingAccountsForTheAvatar()
        {
            // Act
            string label = RoomIndicatorLabel.Build(RoomSource.None, RoomSource.None);

            // Assert
            Assert.AreEqual(RoomIndicatorLabel.NONE, label);
        }

        [Test]
        public void ReportPulseAloneWhenOnlyPulseAnnounced()
        {
            // Arrange
            const RoomSource ANNOUNCED = RoomSource.Pulse;

            // Act
            string label = RoomIndicatorLabel.Build(ANNOUNCED, RoomSource.None);

            // Assert
            Assert.AreEqual($"{RoomIndicatorLabel.PULSE}{nameof(RoomSource.Pulse)}", label);
        }

        [Test]
        public void MarkAParticipantThatNeverAnnouncedAsPresentOnly()
        {
            // Arrange - the steady state while Pulse carries profiles: joined the room, silent on its data channel.
            // Act
            string label = RoomIndicatorLabel.Build(RoomSource.Pulse, RoomSource.Island);

            // Assert
            Assert.AreEqual(
                $"{RoomIndicatorLabel.PRESENT_ONLY}{nameof(RoomSource.Island)} {RoomIndicatorLabel.PULSE}{nameof(RoomSource.Pulse)}",
                label);
        }

        [Test]
        public void MarkAParticipantThatAlsoAnnouncedAsFullyConnected()
        {
            // Act
            string label = RoomIndicatorLabel.Build(RoomSource.Island | RoomSource.Pulse, RoomSource.Island);

            // Assert
            Assert.AreEqual(
                $"{RoomIndicatorLabel.PRESENT_AND_ANNOUNCED}{nameof(RoomSource.Island)} {RoomIndicatorLabel.PULSE}{nameof(RoomSource.Pulse)}",
                label);
        }

        [Test]
        public void MarkAnAnnouncementWithoutAParticipantAsStale()
        {
            // Act
            string label = RoomIndicatorLabel.Build(RoomSource.Island, RoomSource.None);

            // Assert
            Assert.AreEqual($"{RoomIndicatorLabel.ANNOUNCED_ONLY}{nameof(RoomSource.Island)}", label);
        }

        [Test]
        public void ListGatekeeperBeforeIslandBeforePulse()
        {
            // Arrange
            const RoomSource BOTH_LIVE_KIT_ROOMS = RoomSource.Gatekeeper | RoomSource.Island;

            // Act
            string label = RoomIndicatorLabel.Build(BOTH_LIVE_KIT_ROOMS | RoomSource.Pulse, BOTH_LIVE_KIT_ROOMS);

            // Assert
            Assert.AreEqual(
                $"{RoomIndicatorLabel.PRESENT_AND_ANNOUNCED}{nameof(RoomSource.Gatekeeper)}"
                + $" {RoomIndicatorLabel.PRESENT_AND_ANNOUNCED}{nameof(RoomSource.Island)}"
                + $" {RoomIndicatorLabel.PULSE}{nameof(RoomSource.Pulse)}",
                label);
        }

        [Test]
        public void MixPerRoomStatesIndependently()
        {
            // Act - announced over the scene room, merely present in the island.
            string label = RoomIndicatorLabel.Build(RoomSource.Gatekeeper, RoomSource.Gatekeeper | RoomSource.Island);

            // Assert
            Assert.AreEqual(
                $"{RoomIndicatorLabel.PRESENT_AND_ANNOUNCED}{nameof(RoomSource.Gatekeeper)}"
                + $" {RoomIndicatorLabel.PRESENT_ONLY}{nameof(RoomSource.Island)}",
                label);
        }

        [Test]
        public void IgnoreTheChatRoomWhichCarriesNoAvatars()
        {
            // Act
            string label = RoomIndicatorLabel.Build(RoomSource.Chat, RoomSource.Chat);

            // Assert
            Assert.AreEqual(RoomIndicatorLabel.NONE, label);
        }

        /// <remarks>
        ///     These codepoints must stay in sync with the sprite asset the nametags panel resolves text against;
        ///     this pins them so a swap is a deliberate edit rather than a silent one.
        /// </remarks>
        [Test]
        public void PinTheGlyphCodepoints()
        {
            Assert.AreEqual("\U0001F7E2", RoomIndicatorLabel.PRESENT_AND_ANNOUNCED);
            Assert.AreEqual("\U0001F517", RoomIndicatorLabel.PRESENT_ONLY);
            Assert.AreEqual("\U0001F47B", RoomIndicatorLabel.ANNOUNCED_ONLY);
            Assert.AreEqual("\u26A1", RoomIndicatorLabel.PULSE);
        }

        [Test]
        public void NotCarryStateBetweenBuilds()
        {
            // Arrange - the builder is shared, so a missed clear would concatenate consecutive labels.
            RoomIndicatorLabel.Build(RoomSource.Gatekeeper | RoomSource.Island | RoomSource.Pulse, RoomSource.Gatekeeper | RoomSource.Island);

            // Act
            string label = RoomIndicatorLabel.Build(RoomSource.Pulse, RoomSource.None);

            // Assert
            Assert.AreEqual($"{RoomIndicatorLabel.PULSE}{nameof(RoomSource.Pulse)}", label);
        }
    }
}
