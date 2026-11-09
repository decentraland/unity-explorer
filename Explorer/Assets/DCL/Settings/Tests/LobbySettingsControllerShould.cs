using DCL.FeatureFlags;
using DCL.Prefs;
using DCL.Settings.ModuleControllers;
using DCL.Settings.ModuleViews;
using DCL.Settings.Settings;
using Global.AppArgs;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.Settings.Tests
{
    [TestFixture]
    public class LobbySettingsControllerShould
    {
        private const string TOGGLE_VIEW_PREFAB_PATH = "Assets/DCL/Settings/Prefabs/SettingsModuleView_Toggle.prefab";

        // The setting lives in player prefs, which the editor never initializes; see ChatBubblesVisibilityControllerShould
        private static readonly FieldInfo DCL_PREFS_BACKING_FIELD =
            typeof(DCLPlayerPrefs).GetField("dclPrefs", BindingFlags.NonPublic | BindingFlags.Static);

        private IDCLPrefs originalPrefs;
        private GameObject viewGameObject;
        private SettingsToggleModuleView view;
        private FakeSettingsModuleEventListener listener;
        private LobbySettingsController controller;

        [SetUp]
        public void SetUp()
        {
            originalPrefs = (IDCLPrefs)DCL_PREFS_BACKING_FIELD.GetValue(null);
            DCL_PREFS_BACKING_FIELD.SetValue(null, new InMemoryDCLPlayerPrefs());

            // The controller reads and writes the lobby setting through the registry singleton
            FeatureFlagsConfiguration.Reset();
            FeatureFlagsConfiguration.Initialize(new FeatureFlagsConfiguration(FeatureFlagsResultDto.Empty));
            FeaturesRegistry.Reset();
            FeaturesRegistry.Initialize(new FeaturesRegistry(new ApplicationParametersParser(false), localSceneDevelopment: false));

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TOGGLE_VIEW_PREFAB_PATH);
            Assert.IsNotNull(prefab, $"Could not load the toggle module view prefab from {TOGGLE_VIEW_PREFAB_PATH}");
            viewGameObject = Object.Instantiate(prefab);
            view = viewGameObject.GetComponent<SettingsToggleModuleView>();
            Assert.IsNotNull(view, "Toggle prefab is missing its SettingsToggleModuleView component");

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

        [TestCase(false, true)]
        [TestCase(true, false)]
        public void NotifyThePreviousAndNewValueWhenToggled(bool stored, bool toggledTo)
        {
            // Arrange
            FeaturesRegistry.Instance.LobbyEnabledSetting = stored;
            controller = new LobbySettingsController(view, listener);

            // Act
            view.ToggleView.Toggle.isOn = toggledTo;

            // Assert
            Assert.AreEqual(new[] { (stored, toggledTo) }, listener.Changes);
            Assert.AreEqual(toggledTo, FeaturesRegistry.Instance.LobbyEnabledSetting);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ShowTheStoredSettingWithoutNotifying(bool stored)
        {
            // Arrange
            FeaturesRegistry.Instance.LobbyEnabledSetting = stored;

            // Act
            controller = new LobbySettingsController(view, listener);

            // Assert
            Assert.AreEqual(stored, view.ToggleView.Toggle.isOn);
            Assert.IsEmpty(listener.Changes);
        }

        [Test]
        public void StopNotifyingOnceDisposed()
        {
            // Arrange
            FeaturesRegistry.Instance.LobbyEnabledSetting = false;
            controller = new LobbySettingsController(view, listener);
            controller.Dispose();

            // Act
            view.ToggleView.Toggle.isOn = true;

            // Assert
            Assert.IsEmpty(listener.Changes);
            Assert.IsFalse(FeaturesRegistry.Instance.LobbyEnabledSetting);
        }

        private sealed class FakeSettingsModuleEventListener : ISettingsModuleEventListener
        {
            public readonly List<(bool previous, bool current)> Changes = new ();

            public event Action<ChatBubbleVisibilitySettings> ChatBubblesVisibilityChanged = delegate { };

            public event Action<bool, bool> LobbyEnabledChanged = delegate { };

            public void NotifyChatBubblesVisibilityChanged(ChatBubbleVisibilitySettings newVisibility) =>
                ChatBubblesVisibilityChanged.Invoke(newVisibility);

            public void NotifyLobbyEnabledChanged(bool previousValue, bool newValue)
            {
                Changes.Add((previousValue, newValue));
                LobbyEnabledChanged.Invoke(previousValue, newValue);
            }
        }
    }
}
