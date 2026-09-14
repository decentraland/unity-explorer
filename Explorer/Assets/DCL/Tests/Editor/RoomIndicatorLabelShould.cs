using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Connections.Systems.RoomIndicator;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

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

        [TestCase(RoomIndicatorLabel.PRESENT_AND_ANNOUNCED, "1f7e2")]
        [TestCase(RoomIndicatorLabel.PRESENT_ONLY, "1f517")]
        [TestCase(RoomIndicatorLabel.ANNOUNCED_ONLY, "1f47b")]
        [TestCase(RoomIndicatorLabel.PULSE, "26a1")]
        public void ResolveNamedSpritesFromTheNametagPrefab(string tag, string spriteName)
        {
            // Arrange
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DCL/NameTags/Assets/NametagUIDocument.prefab");
            PanelSettings panel = prefab.GetComponent<UIDocument>().panelSettings;
            SpriteAsset sprites = panel.textSettings.defaultSpriteAsset;

            // Act
            int index = sprites.GetSpriteIndexFromName(spriteName);

            // Assert
            Assert.AreEqual($"<sprite name=\"{spriteName}\">", tag);
            Assert.That(index, Is.GreaterThanOrEqualTo(0));
            Assert.That(sprites.spriteSheet, Is.Not.Null);
            Assert.That(sprites.material, Is.Not.Null);
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
