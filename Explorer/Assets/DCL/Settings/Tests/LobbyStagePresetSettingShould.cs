using DCL.Prefs;
using DCL.Settings.Settings;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.Settings.Tests
{
    [TestFixture]
    public class LobbyStagePresetSettingShould
    {
        private static readonly string[] NAMES = { "Default", "Arena", "Night" };

        // The setting lives in player prefs, which the editor never initializes; see ChatBubblesVisibilityControllerShould
        private static readonly FieldInfo DCL_PREFS_BACKING_FIELD =
            typeof(DCLPlayerPrefs).GetField("dclPrefs", BindingFlags.NonPublic | BindingFlags.Static)!;

        private IDCLPrefs originalPrefs = null!;

        [SetUp]
        public void SetUp()
        {
            originalPrefs = (IDCLPrefs)DCL_PREFS_BACKING_FIELD.GetValue(null);
            DCL_PREFS_BACKING_FIELD.SetValue(null, new InMemoryDCLPlayerPrefs());
        }

        [TearDown]
        public void TearDown()
        {
            DCL_PREFS_BACKING_FIELD.SetValue(null, originalPrefs);
        }

        [Test]
        public void DefaultToTheFirstEntryWhenNothingIsStored()
        {
            // Act
            var setting = new LobbyStagePresetSetting(NAMES);

            // Assert
            Assert.AreEqual(0, setting.SelectedIndex);
        }

        [Test]
        public void RestoreTheStoredEntryByName()
        {
            // Arrange
            DCLPlayerPrefs.SetString(DCLPrefKeys.SETTINGS_LOBBY_STAGE_PRESET, "Night");

            // Act
            var setting = new LobbyStagePresetSetting(NAMES);

            // Assert
            Assert.AreEqual(2, setting.SelectedIndex);
        }

        [Test]
        public void FallBackToTheFirstEntryWhenTheStoredNameIsUnknown()
        {
            // Arrange
            DCLPlayerPrefs.SetString(DCLPrefKeys.SETTINGS_LOBBY_STAGE_PRESET, "Removed");

            // Act
            var setting = new LobbyStagePresetSetting(NAMES);

            // Assert
            Assert.AreEqual(0, setting.SelectedIndex);
        }

        [Test]
        public void PersistTheNameAndNotifyWhenSelecting()
        {
            // Arrange
            var setting = new LobbyStagePresetSetting(NAMES);
            var notified = new List<int>();
            setting.SelectedChanged += notified.Add;

            // Act
            setting.Select(1);

            // Assert
            Assert.AreEqual(1, setting.SelectedIndex);
            Assert.AreEqual("Arena", DCLPlayerPrefs.GetString(DCLPrefKeys.SETTINGS_LOBBY_STAGE_PRESET));
            Assert.AreEqual(new[] { 1 }, notified);
        }

        [TestCase(-1)]
        [TestCase(3)]
        [TestCase(0)]
        public void IgnoreOutOfRangeAndUnchangedSelections(int index)
        {
            // Arrange
            var setting = new LobbyStagePresetSetting(NAMES);
            var notified = new List<int>();
            setting.SelectedChanged += notified.Add;

            // Act
            setting.Select(index);

            // Assert
            Assert.AreEqual(0, setting.SelectedIndex);
            Assert.IsFalse(DCLPlayerPrefs.HasKey(DCLPrefKeys.SETTINGS_LOBBY_STAGE_PRESET));
            Assert.IsEmpty(notified);
        }

        [Test]
        public void HaveNoBackdropUntilOneIsSet()
        {
            // Arrange
            var setting = new LobbyStagePresetSetting(NAMES);
            var notified = new List<int>();
            setting.BackdropChanged += notified.Add;
            var texture = new Texture2D(2, 2);

            // Act
            Texture? before = setting.BackdropOf(1);
            setting.SetBackdrop(1, texture);

            // Assert
            Assert.IsNull(before);
            Assert.AreEqual(texture, setting.BackdropOf(1));
            Assert.IsNull(setting.BackdropOf(0));
            Assert.AreEqual(new[] { 1 }, notified);

            Object.DestroyImmediate(texture);
        }

        [Test]
        public void HaveNoSelectionWithoutEntries()
        {
            // Arrange
            var setting = new LobbyStagePresetSetting(Array.Empty<string>());
            var notified = new List<int>();
            setting.SelectedChanged += notified.Add;

            // Act
            setting.Select(0);

            // Assert
            Assert.AreEqual(-1, setting.SelectedIndex);
            Assert.IsEmpty(notified);
        }
    }
}
