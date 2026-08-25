using DCL.Prefs;
using DCL.Settings.ModuleControllers;
using DCL.Settings.ModuleViews;
using DCL.Settings.Settings;
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.Settings.Tests
{
    [TestFixture]
    public class ChatBubblesVisibilityControllerShould
    {
        private const string DROPDOWN_VIEW_PREFAB_PATH = "Assets/DCL/Settings/Prefabs/SettingsModuleView_Dropdown.prefab";

        // DCLPlayerPrefs gates every read behind a private static backing field that is normally
        // populated by a [RuntimeInitializeOnLoadMethod] at BeforeSceneLoad. EditMode tests never
        // enter Play mode, so nothing populates it; reflection swaps in a hermetic in-memory
        // store for the duration of the test instead of depending on that runtime hook or
        // touching any real on-disk/native prefs state.
        private static readonly FieldInfo DCL_PREFS_BACKING_FIELD =
            typeof(DCLPlayerPrefs).GetField("dclPrefs", BindingFlags.NonPublic | BindingFlags.Static);

        private IDCLPrefs originalPrefs;
        private GameObject viewGameObject;
        private ChatSettingsAsset chatSettingsAsset;
        private SettingsFeatureController controller;

        [SetUp]
        public void SetUp()
        {
            Assert.IsNotNull(DCL_PREFS_BACKING_FIELD, "DCLPlayerPrefs no longer has a 'dclPrefs' backing field — update this test's reflection target.");

            originalPrefs = (IDCLPrefs) DCL_PREFS_BACKING_FIELD.GetValue(null);
            DCL_PREFS_BACKING_FIELD.SetValue(null, new InMemoryDCLPlayerPrefs());

            chatSettingsAsset = ScriptableObject.CreateInstance<ChatSettingsAsset>();

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DROPDOWN_VIEW_PREFAB_PATH);
            Assert.IsNotNull(prefab, $"Could not load the dropdown module view prefab from {DROPDOWN_VIEW_PREFAB_PATH}");
            viewGameObject = Object.Instantiate(prefab);
        }

        [TearDown]
        public void TearDown()
        {
            controller?.Dispose();

            if (viewGameObject != null) Object.DestroyImmediate(viewGameObject);
            if (chatSettingsAsset != null) Object.DestroyImmediate(chatSettingsAsset);

            DCL_PREFS_BACKING_FIELD.SetValue(null, originalPrefs);
        }

        [TestCase(ChatBubbleVisibilitySettings.None)]
        [TestCase(ChatBubbleVisibilitySettings.NearbyOnly)]
        [TestCase(ChatBubbleVisibilitySettings.All)]
        public void ApplyStoredVisibilityToSettingsAssetOnConstruction(ChatBubbleVisibilitySettings expected)
        {
            DCLPlayerPrefs.SetInt(DCLPrefKeys.SETTINGS_CHAT_BUBBLES_VISIBILITY, (int) expected, save: false);

            var view = viewGameObject.GetComponent<SettingsDropdownModuleView>();
            Assert.IsNotNull(view, "Dropdown prefab is missing its SettingsDropdownModuleView component");

            var eventListener = new FakeSettingsModuleEventListener();

            controller = new ChatBubblesVisibilityController(view, chatSettingsAsset, eventListener);

            Assert.AreEqual(
                expected,
                chatSettingsAsset.chatBubblesVisibilitySettings,
                $"The stored '{expected}' preference was not applied to ChatSettingsAsset at construction time.");
        }

        private sealed class FakeSettingsModuleEventListener : ISettingsModuleEventListener
        {
            public event Action<ChatBubbleVisibilitySettings> ChatBubblesVisibilityChanged = delegate { };

            public void NotifyChatBubblesVisibilityChanged(ChatBubbleVisibilitySettings newVisibility) =>
                ChatBubblesVisibilityChanged.Invoke(newVisibility);
        }
    }
}
