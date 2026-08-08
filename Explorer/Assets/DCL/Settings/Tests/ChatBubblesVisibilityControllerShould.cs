using DCL.Prefs;
using DCL.Settings;
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
    /// <summary>
    /// Regression coverage for #9562: a chat-bubbles-visibility preference saved in a previous
    /// session must be applied to the live <see cref="ChatSettingsAsset"/> the moment
    /// <see cref="ChatBubblesVisibilityController"/> is constructed at boot — not merely
    /// reflected in the dropdown widget while the runtime asset stays at its serialized default.
    /// </summary>
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

        [Test]
        public void ApplyStoredNoneVisibilityToSettingsAssetOnConstruction()
        {
            // Arrange: a previous session saved "None" (index 0) for the chat bubbles setting.
            DCLPlayerPrefs.SetInt(DCLPrefKeys.SETTINGS_CHAT_BUBBLES_VISIBILITY, (int) ChatBubbleVisibilitySettings.None, save: false);

            var view = viewGameObject.GetComponent<SettingsDropdownModuleView>();
            Assert.IsNotNull(view, "Dropdown prefab is missing its SettingsDropdownModuleView component");

            var eventListener = new FakeSettingsModuleEventListener();

            // Act: constructing the controller mirrors DropdownModuleBinding.CreateChatBubblesController,
            // which runs eagerly during plugin bootstrap — before any chat message can arrive.
            controller = new ChatBubblesVisibilityController(view, chatSettingsAsset, eventListener);

            // Assert: ChatWorldBubbleService gates every bubble spawn on this exact field. It must
            // already be None right after construction, not just the dropdown's displayed value.
            Assert.AreEqual(
                ChatBubbleVisibilitySettings.None,
                chatSettingsAsset.chatBubblesVisibilitySettings,
                "The stored 'None' preference was not applied to ChatSettingsAsset at construction time.");
        }

        private sealed class FakeSettingsModuleEventListener : ISettingsModuleEventListener
        {
            public event Action<ChatBubbleVisibilitySettings> ChatBubblesVisibilityChanged;

            public void NotifyChatBubblesVisibilityChanged(ChatBubbleVisibilitySettings newVisibility) =>
                ChatBubblesVisibilityChanged?.Invoke(newVisibility);
        }
    }
}
