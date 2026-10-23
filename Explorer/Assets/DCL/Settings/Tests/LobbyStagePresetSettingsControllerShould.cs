using DCL.FeatureFlags;
using DCL.Prefs;
using DCL.Settings.ModuleControllers;
using DCL.Settings.ModuleViews;
using DCL.Settings.Settings;
using Global.AppArgs;
using NUnit.Framework;
using System;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.Settings.Tests
{
    [TestFixture]
    public class LobbyStagePresetSettingsControllerShould
    {
        private const string DROPDOWN_VIEW_PREFAB_PATH = "Assets/DCL/Settings/Prefabs/SettingsModuleView_Dropdown.prefab";
        private static readonly string[] NAMES = { "Default", "Arena", "Night" };

        // The settings live in player prefs, which the editor never initializes; see ChatBubblesVisibilityControllerShould
        private static readonly FieldInfo DCL_PREFS_BACKING_FIELD =
            typeof(DCLPlayerPrefs).GetField("dclPrefs", BindingFlags.NonPublic | BindingFlags.Static);

        private IDCLPrefs originalPrefs = null!;
        private GameObject viewGameObject = null!;
        private SettingsDropdownModuleView view = null!;
        private FakeSettingsModuleEventListener listener = null!;
        private LobbyStagePresetSettingsController? controller;

        [SetUp]
        public void SetUp()
        {
            originalPrefs = (IDCLPrefs)DCL_PREFS_BACKING_FIELD.GetValue(null);
            DCL_PREFS_BACKING_FIELD.SetValue(null, new InMemoryDCLPlayerPrefs());

            FeatureFlagsConfiguration.Reset();
            FeatureFlagsConfiguration.Initialize(new FeatureFlagsConfiguration(FeatureFlagsResultDto.Empty));
            FeaturesRegistry.Reset();
            FeaturesRegistry.Initialize(new FeaturesRegistry(new ApplicationParametersParser(false), localSceneDevelopment: false));

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DROPDOWN_VIEW_PREFAB_PATH);
            Assert.IsNotNull(prefab, $"Could not load the dropdown module view prefab from {DROPDOWN_VIEW_PREFAB_PATH}");
            viewGameObject = Object.Instantiate(prefab);
            view = viewGameObject.GetComponent<SettingsDropdownModuleView>();
            Assert.IsNotNull(view, "Dropdown prefab is missing its SettingsDropdownModuleView component");

            listener = new FakeSettingsModuleEventListener();
        }

        [TearDown]
        public void TearDown()
        {
            controller?.Dispose();

            if (viewGameObject != null) Object.DestroyImmediate(viewGameObject);

            FeaturesRegistry.Reset();
            FeatureFlagsConfiguration.Reset();
            DCL_PREFS_BACKING_FIELD.SetValue(null, originalPrefs);
        }

        [Test]
        public void ListTheConfiguredNamesAndShowTheStoredChoice()
        {
            // Arrange
            DCLPlayerPrefs.SetString(DCLPrefKeys.SETTINGS_LOBBY_STAGE_PRESET, "Night");
            var setting = new LobbyStagePresetSetting(NAMES);

            // Act
            controller = new LobbyStagePresetSettingsController(view, setting, listener);

            // Assert
            TMP_Dropdown dropdown = view.DropdownView.Dropdown;
            Assert.AreEqual(NAMES.Length, dropdown.options.Count);

            for (var i = 0; i < NAMES.Length; i++)
                Assert.AreEqual(NAMES[i], dropdown.options[i].text);

            Assert.AreEqual(2, dropdown.value);
            Assert.AreEqual(2, setting.SelectedIndex);
        }

        [Test]
        public void SelectTheEntryPickedInTheDropdown()
        {
            // Arrange
            var setting = new LobbyStagePresetSetting(NAMES);
            controller = new LobbyStagePresetSettingsController(view, setting, listener);

            // Act
            view.DropdownView.Dropdown.value = 1;

            // Assert
            Assert.AreEqual(1, setting.SelectedIndex);
            Assert.AreEqual("Arena", DCLPlayerPrefs.GetString(DCLPrefKeys.SETTINGS_LOBBY_STAGE_PRESET));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void BeInteractableOnlyWhileTheLobbyIsEnabled(bool lobbyEnabled)
        {
            // Arrange
            FeaturesRegistry.Instance.LobbyEnabledSetting = lobbyEnabled;

            // Act
            controller = new LobbyStagePresetSettingsController(view, new LobbyStagePresetSetting(NAMES), listener);

            // Assert
            Assert.AreEqual(lobbyEnabled, view.DropdownView.Dropdown.interactable);
        }

        [Test]
        public void FollowTheLobbyToggleWhileOpen()
        {
            // Arrange
            FeaturesRegistry.Instance.LobbyEnabledSetting = false;
            controller = new LobbyStagePresetSettingsController(view, new LobbyStagePresetSetting(NAMES), listener);

            // Act
            listener.NotifyLobbyEnabledChanged(false, true);
            bool afterEnable = view.DropdownView.Dropdown.interactable;
            listener.NotifyLobbyEnabledChanged(true, false);

            // Assert
            Assert.IsTrue(afterEnable);
            Assert.IsFalse(view.DropdownView.Dropdown.interactable);
        }

        [Test]
        public void StayNonInteractableWithoutEntries()
        {
            // Arrange
            FeaturesRegistry.Instance.LobbyEnabledSetting = true;

            // Act
            controller = new LobbyStagePresetSettingsController(view, new LobbyStagePresetSetting(Array.Empty<string>()), listener);
            listener.NotifyLobbyEnabledChanged(false, true);

            // Assert
            Assert.IsFalse(view.DropdownView.Dropdown.interactable);
            Assert.IsEmpty(view.DropdownView.Dropdown.options);
        }

        [Test]
        public void StopReactingOnceDisposed()
        {
            // Arrange
            FeaturesRegistry.Instance.LobbyEnabledSetting = false;
            var setting = new LobbyStagePresetSetting(NAMES);
            controller = new LobbyStagePresetSettingsController(view, setting, listener);
            controller.Dispose();

            // Act
            listener.NotifyLobbyEnabledChanged(false, true);
            view.DropdownView.Dropdown.value = 2;

            // Assert
            Assert.IsFalse(view.DropdownView.Dropdown.interactable);
            Assert.AreEqual(0, setting.SelectedIndex);
        }

        private sealed class FakeSettingsModuleEventListener : ISettingsModuleEventListener
        {
            public event Action<ChatBubbleVisibilitySettings> ChatBubblesVisibilityChanged = delegate { };

            public event Action<bool, bool> LobbyEnabledChanged = delegate { };

            public void NotifyChatBubblesVisibilityChanged(ChatBubbleVisibilitySettings newVisibility) =>
                ChatBubblesVisibilityChanged.Invoke(newVisibility);

            public void NotifyLobbyEnabledChanged(bool previousValue, bool newValue) =>
                LobbyEnabledChanged.Invoke(previousValue, newValue);
        }
    }
}
