using DCL.FeatureFlags;
using DCL.Prefs;
using DCL.Settings.ModuleControllers;
using DCL.Settings.ModuleViews;
using DCL.Settings.Settings;
using Global.AppArgs;
using NUnit.Framework;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace DCL.Settings.Tests
{
    [TestFixture]
    public class LobbyStagePresetSettingsControllerShould
    {
        private const string VIEW_PREFAB_PATH = "Assets/DCL/Settings/Prefabs/SettingsModuleView_LobbyStagePreset.prefab";
        private static readonly string[] NAMES = { "Default", "Arena", "Night" };

        // The settings live in player prefs, which the editor never initializes; see ChatBubblesVisibilityControllerShould
        private static readonly FieldInfo DCL_PREFS_BACKING_FIELD =
            typeof(DCLPlayerPrefs).GetField("dclPrefs", BindingFlags.NonPublic | BindingFlags.Static)!;

        private IDCLPrefs originalPrefs = null!;
        private GameObject viewGameObject = null!;
        private SettingsLobbyStagePresetModuleView view = null!;
        private Texture2D textureA = null!;
        private Texture2D textureB = null!;
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

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VIEW_PREFAB_PATH);
            Assert.IsNotNull(prefab, $"Could not load the module view prefab from {VIEW_PREFAB_PATH}");
            viewGameObject = Object.Instantiate(prefab);
            view = viewGameObject.GetComponent<SettingsLobbyStagePresetModuleView>();
            Assert.IsNotNull(view, "Module view prefab is missing its SettingsLobbyStagePresetModuleView component");

            textureA = new Texture2D(2, 2);
            textureB = new Texture2D(4, 2);
        }

        [TearDown]
        public void TearDown()
        {
            controller?.Dispose();

            if (viewGameObject != null) Object.DestroyImmediate(viewGameObject);
            Object.DestroyImmediate(textureA);
            Object.DestroyImmediate(textureB);

            FeaturesRegistry.Reset();
            FeatureFlagsConfiguration.Reset();
            DCL_PREFS_BACKING_FIELD.SetValue(null, originalPrefs);
        }

        [Test]
        public void CreateOneEntryPerNameWithItsTooltip()
        {
            // Arrange
            var setting = new LobbyStagePresetSetting(NAMES);

            // Act
            controller = new LobbyStagePresetSettingsController(view, setting);

            // Assert
            Assert.AreEqual(NAMES.Length, view.EntriesContainer.childCount);

            for (var i = 0; i < NAMES.Length; i++)
                Assert.AreEqual(NAMES[i], Entry(i).GetComponentInChildren<TMP_Text>(true).text);
        }

        [Test]
        public void MarkTheStoredChoiceAndPreviewItsBackdrop()
        {
            // Arrange
            DCLPlayerPrefs.SetString(DCLPrefKeys.SETTINGS_LOBBY_STAGE_PRESET, "Night");
            var setting = new LobbyStagePresetSetting(NAMES);
            setting.SetBackdrop(2, textureA);

            // Act
            controller = new LobbyStagePresetSettingsController(view, setting);

            // Assert
            AssertOnlySelected(2);
            Assert.AreEqual(textureA, view.PreviewTexture);
            Assert.IsTrue(view.Preview.enabled);
            Assert.AreEqual(textureA, Entry(2).Thumbnail.texture);
            Assert.AreEqual(1f, Entry(2).ThumbnailFitter.aspectRatio);
        }

        [Test]
        public void SelectTheClickedEntry()
        {
            // Arrange
            var setting = new LobbyStagePresetSetting(NAMES);
            controller = new LobbyStagePresetSettingsController(view, setting);

            // Act
            Entry(1).Button.onClick.Invoke();

            // Assert
            Assert.AreEqual(1, setting.SelectedIndex);
            Assert.AreEqual("Arena", DCLPlayerPrefs.GetString(DCLPrefKeys.SETTINGS_LOBBY_STAGE_PRESET));
            AssertOnlySelected(1);
        }

        [Test]
        public void ShowBackdropsAsTheyArrive()
        {
            // Arrange
            var setting = new LobbyStagePresetSetting(NAMES);
            controller = new LobbyStagePresetSettingsController(view, setting);
            Assert.IsFalse(view.Preview.enabled);
            Assert.IsFalse(Entry(0).Thumbnail.enabled);

            // Act
            setting.SetBackdrop(1, textureB);
            setting.SetBackdrop(0, textureA);

            // Assert
            Assert.AreEqual(textureA, Entry(0).Thumbnail.texture);
            Assert.AreEqual(textureB, Entry(1).Thumbnail.texture);
            Assert.AreEqual(textureA, view.PreviewTexture);
            Assert.AreEqual(2f, Entry(1).ThumbnailFitter.aspectRatio);
            Assert.AreEqual(1f, view.PreviewFitter.aspectRatio);
            Assert.IsTrue(view.Preview.enabled);
        }

        [Test]
        public void SwitchThePreviewWithTheSelection()
        {
            // Arrange
            var setting = new LobbyStagePresetSetting(NAMES);
            setting.SetBackdrop(0, textureA);
            setting.SetBackdrop(1, textureB);
            controller = new LobbyStagePresetSettingsController(view, setting);

            // Act
            setting.Select(1);

            // Assert
            Assert.AreEqual(textureB, view.PreviewTexture);
            AssertOnlySelected(1);
        }

        [Test]
        public void PreviewTheHoveredEntryAndFallBackToTheSelection()
        {
            // Arrange
            var setting = new LobbyStagePresetSetting(NAMES);
            setting.SetBackdrop(0, textureA);
            setting.SetBackdrop(1, textureB);
            controller = new LobbyStagePresetSettingsController(view, setting);
            var pointer = new PointerEventData(null);

            // Act
            Entry(1).OnPointerEnter(pointer);
            Texture? whileHovered = view.PreviewTexture;
            Entry(1).OnPointerExit(pointer);

            // Assert
            Assert.AreEqual(textureB, whileHovered);
            Assert.AreEqual(textureA, view.PreviewTexture);
            Assert.AreEqual(0, setting.SelectedIndex);
        }

        [Test]
        public void KeepTheHoveredPreviewWhenAnotherEntryIsSelected()
        {
            // Arrange
            var setting = new LobbyStagePresetSetting(NAMES);
            setting.SetBackdrop(0, textureA);
            setting.SetBackdrop(1, textureB);
            controller = new LobbyStagePresetSettingsController(view, setting);
            Entry(1).OnPointerEnter(new PointerEventData(null));

            // Act
            Entry(2).Button.onClick.Invoke();

            // Assert
            Assert.AreEqual(2, setting.SelectedIndex);
            Assert.AreEqual(textureB, view.PreviewTexture);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void StayInteractableRegardlessOfTheLobbyToggle(bool lobbyEnabled)
        {
            // Arrange
            FeaturesRegistry.Instance.LobbyEnabledSetting = lobbyEnabled;

            // Act
            controller = new LobbyStagePresetSettingsController(view, new LobbyStagePresetSetting(NAMES));

            // Assert
            Assert.IsTrue(view.CanvasGroup.interactable);
            Assert.IsTrue(view.CanvasGroup.blocksRaycasts);
        }

        [Test]
        public void StopReactingOnceDisposed()
        {
            // Arrange
            var setting = new LobbyStagePresetSetting(NAMES);
            controller = new LobbyStagePresetSettingsController(view, setting);
            controller.Dispose();

            // Act
            Entry(2).Button.onClick.Invoke();
            Entry(2).OnPointerEnter(new PointerEventData(null));
            setting.Select(1);
            setting.SetBackdrop(1, textureB);

            // Assert
            Assert.AreEqual(1, setting.SelectedIndex);
            AssertOnlySelected(0);
            Assert.IsNull(Entry(1).Thumbnail.texture);
            Assert.IsNull(view.PreviewTexture);
        }

        private LobbyStagePresetEntryView Entry(int index) =>
            view.EntriesContainer.GetChild(index).GetComponent<LobbyStagePresetEntryView>();

        private void AssertOnlySelected(int selected)
        {
            for (var i = 0; i < NAMES.Length; i++)
            {
                Assert.AreEqual(i == selected, Entry(i).SelectedOutline.activeSelf, $"outline of entry {i}");
                Assert.AreEqual(i == selected, Entry(i).Checkmark.activeSelf, $"checkmark of entry {i}");
            }
        }
    }
}
