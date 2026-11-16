using NUnit.Framework;
using System;
using StagePresetEntry = DCL.PluginSystem.Global.LobbyPlugin.LobbyPluginSettings.StagePresetEntry;

namespace DCL.PluginSystem.Global.Tests
{
    [TestFixture]
    public class LobbyStagePresetAvailabilityShould
    {
        private static readonly StagePresetEntry[] ENTRIES =
        {
            new () { Name = "Default" },
            new () { Name = "Arena" },
            new () { Name = "Night" },
        };

        [Test]
        public void OfferEveryEntryInConfiguredOrderWithoutConfig()
        {
            // Act
            StagePresetEntry[] resolved = LobbyStagePresetAvailability.Resolve(ENTRIES, null);

            // Assert
            Assert.AreEqual(ENTRIES, resolved);
        }

        [Test]
        public void KeepOnlyAvailableEntriesInConfiguredOrder()
        {
            // Arrange
            var config = new LobbyStagePresetAvailability.Config { available = new[] { "Night", "Default" } };

            // Act
            StagePresetEntry[] resolved = LobbyStagePresetAvailability.Resolve(ENTRIES, config);

            // Assert
            Assert.AreEqual(new[] { ENTRIES[0], ENTRIES[2] }, resolved);
        }

        [Test]
        public void SkipAvailableNamesThatAreNotConfigured()
        {
            // Arrange
            var config = new LobbyStagePresetAvailability.Config { available = new[] { "Arena", "Unknown" } };

            // Act
            StagePresetEntry[] resolved = LobbyStagePresetAvailability.Resolve(ENTRIES, config);

            // Assert
            Assert.AreEqual(new[] { ENTRIES[1] }, resolved);
        }

        [Test]
        public void OfferEveryEntryWhenNoAvailableNameIsConfigured()
        {
            // Arrange
            var config = new LobbyStagePresetAvailability.Config { available = new[] { "Unknown" } };

            // Act
            StagePresetEntry[] resolved = LobbyStagePresetAvailability.Resolve(ENTRIES, config);

            // Assert
            Assert.AreEqual(ENTRIES, resolved);
        }

        [Test]
        public void OfferEveryEntryWhenAvailableIsEmpty()
        {
            // Arrange
            var config = new LobbyStagePresetAvailability.Config { available = Array.Empty<string>() };

            // Act
            StagePresetEntry[] resolved = LobbyStagePresetAvailability.Resolve(ENTRIES, config);

            // Assert
            Assert.AreEqual(ENTRIES, resolved);
        }

        [Test]
        public void MoveTheDefaultFirstAndKeepTheRestInOrder()
        {
            // Arrange
            var config = new LobbyStagePresetAvailability.Config { @default = "Night" };

            // Act
            StagePresetEntry[] resolved = LobbyStagePresetAvailability.Resolve(ENTRIES, config);

            // Assert
            Assert.AreEqual(new[] { ENTRIES[2], ENTRIES[0], ENTRIES[1] }, resolved);
        }

        [Test]
        public void MoveTheDefaultFirstAmongAvailableEntries()
        {
            // Arrange
            var config = new LobbyStagePresetAvailability.Config { @default = "Night", available = new[] { "Arena", "Night" } };

            // Act
            StagePresetEntry[] resolved = LobbyStagePresetAvailability.Resolve(ENTRIES, config);

            // Assert
            Assert.AreEqual(new[] { ENTRIES[2], ENTRIES[1] }, resolved);
        }

        [Test]
        public void KeepTheOrderWhenTheDefaultIsNotAvailable()
        {
            // Arrange
            var config = new LobbyStagePresetAvailability.Config { @default = "Default", available = new[] { "Arena", "Night" } };

            // Act
            StagePresetEntry[] resolved = LobbyStagePresetAvailability.Resolve(ENTRIES, config);

            // Assert
            Assert.AreEqual(new[] { ENTRIES[1], ENTRIES[2] }, resolved);
        }

        [Test]
        public void LeaveTheConfiguredArrayUntouched()
        {
            // Arrange
            StagePresetEntry[] entries = (StagePresetEntry[])ENTRIES.Clone();
            var config = new LobbyStagePresetAvailability.Config { @default = "Night" };

            // Act
            LobbyStagePresetAvailability.Resolve(entries, config);

            // Assert
            Assert.AreEqual(ENTRIES, entries);
        }
    }
}
