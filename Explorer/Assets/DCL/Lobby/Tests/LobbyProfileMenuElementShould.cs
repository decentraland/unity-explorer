using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Utility.UIToolkit;

namespace DCL.Lobby.Tests
{
    [TestFixture]
    public class LobbyProfileMenuElementShould
    {
        private const string TEMPLATE_PATH = "Assets/DCL/Lobby/UIToolkit/LobbyProfileMenu.uxml";

        private LobbyProfileMenuElement menu = null!;
        private GameObject? documentGameObject;
        private PanelSettings? panelSettings;

        [SetUp]
        public void SetUp()
        {
            menu = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TEMPLATE_PATH).InstantiateForElement<LobbyProfileMenuElement>();
        }

        [TearDown]
        public void TearDown()
        {
            if (documentGameObject != null)
                Object.DestroyImmediate(documentGameObject);

            if (panelSettings != null)
                Object.DestroyImmediate(panelSettings);
        }

        [Test]
        public void StartClosedAndToggleWithOpenAndClose()
        {
            // Assert
            Assert.IsFalse(menu.IsOpen);

            // Act
            menu.Open();

            // Assert
            Assert.IsTrue(menu.IsOpen);
            Assert.IsTrue(menu.IsDisplayed());

            // Act
            menu.Close();

            // Assert
            Assert.IsFalse(menu.IsOpen);
        }

        [Test]
        public void ApplyWhatItWasGivenOnceItsChildrenAttach()
        {
            // Arrange
            menu.UserName = "Mojito";
            menu.UserNameColor = Color.magenta;
            menu.Address = "0x10e...a7a92";

            // Act
            AttachToPanel();

            // Assert
            Assert.AreEqual("Mojito", menu.Q<Label>("Name").text);
            Assert.AreEqual(Color.magenta, menu.Q<Label>("Name").style.color.value);
            Assert.AreEqual("0x10e...a7a92", menu.Q<Label>("Address").text);
        }

        [Test]
        public void ApplyWhatItIsGivenWhileAttached()
        {
            // Arrange
            AttachToPanel();

            // Act
            menu.UserName = "Mojito";
            menu.UserNameColor = Color.magenta;
            menu.Address = "0x10e...a7a92";

            // Assert
            Assert.AreEqual("Mojito", menu.Q<Label>("Name").text);
            Assert.AreEqual(Color.magenta, menu.Q<Label>("Name").style.color.value);
            Assert.AreEqual("0x10e...a7a92", menu.Q<Label>("Address").text);
        }

        [Test]
        public void MarkItsStatesWithModifiersWithoutAttaching()
        {
            // Act
            menu.IsVerified = true;
            menu.IsCopiedShown = true;
            menu.CanReportBug = true;

            // Assert
            Assert.IsTrue(menu.ClassListContains("lobby-profile-menu--verified"));
            Assert.IsTrue(menu.ClassListContains("lobby-profile-menu--copied"));
            Assert.IsTrue(menu.ClassListContains("lobby-profile-menu--with-bug-report"));

            // Act
            menu.IsVerified = false;
            menu.IsCopiedShown = false;
            menu.CanReportBug = false;

            // Assert
            Assert.IsFalse(menu.ClassListContains("lobby-profile-menu--verified"));
            Assert.IsFalse(menu.ClassListContains("lobby-profile-menu--copied"));
            Assert.IsFalse(menu.ClassListContains("lobby-profile-menu--with-bug-report"));
        }

        [Test]
        public void DrawThePictureWithItsProfileColor()
        {
            // Arrange
            AttachToPanel();
            var sprite = Sprite.Create(new Texture2D(2, 2), new Rect(0, 0, 2, 2), Vector2.zero);

            try
            {
                // Act
                menu.SetPicture(sprite, Color.red, loading: false);

                // Assert
                VisualElement picture = menu.Q<VisualElement>("Picture");
                Assert.AreEqual(sprite.texture, picture.style.backgroundImage.value.texture, "A full-rect sprite is drawn as its texture so the cover crops it");
                Assert.AreEqual(Color.red, picture.style.backgroundColor.value);
                Assert.IsFalse(menu.ClassListContains("lobby-profile-menu__picture--loading"));

                // Act
                menu.SetPicture(null, Color.blue, loading: true);

                // Assert
                Assert.AreEqual(StyleKeyword.Null, picture.style.backgroundImage.keyword);
                Assert.IsTrue(menu.ClassListContains("lobby-profile-menu__picture--loading"));
            }
            finally
            {
                Object.DestroyImmediate(sprite.texture);
                Object.DestroyImmediate(sprite);
            }
        }

        [TestCase("CopyAddress", nameof(LobbyProfileMenuElement.CopyAddressClicked))]
        [TestCase("ProfileItem", nameof(LobbyProfileMenuElement.ProfileClicked))]
        [TestCase("SettingsItem", nameof(LobbyProfileMenuElement.SettingsClicked))]
        [TestCase("ReportBugItem", nameof(LobbyProfileMenuElement.ReportBugClicked))]
        [TestCase("SignOutItem", nameof(LobbyProfileMenuElement.SignOutClicked))]
        [TestCase("ExitItem", nameof(LobbyProfileMenuElement.ExitClicked))]
        [TestCase("TermsLink", nameof(LobbyProfileMenuElement.TermsClicked))]
        [TestCase("PrivacyLink", nameof(LobbyProfileMenuElement.PrivacyClicked))]
        public void ReportOnlyTheClickedItem(string buttonName, string expectedAction)
        {
            // Arrange
            AttachToPanel();
            string? reported = null;
            menu.CopyAddressClicked = () => reported = nameof(LobbyProfileMenuElement.CopyAddressClicked);
            menu.ProfileClicked = () => reported = nameof(LobbyProfileMenuElement.ProfileClicked);
            menu.SettingsClicked = () => reported = nameof(LobbyProfileMenuElement.SettingsClicked);
            menu.ReportBugClicked = () => reported = nameof(LobbyProfileMenuElement.ReportBugClicked);
            menu.SignOutClicked = () => reported = nameof(LobbyProfileMenuElement.SignOutClicked);
            menu.ExitClicked = () => reported = nameof(LobbyProfileMenuElement.ExitClicked);
            menu.TermsClicked = () => reported = nameof(LobbyProfileMenuElement.TermsClicked);
            menu.PrivacyClicked = () => reported = nameof(LobbyProfileMenuElement.PrivacyClicked);
            var outside = 0;
            menu.OutsideClicked = () => outside++;

            // Act
            Submit(menu.Q<Button>(buttonName));

            // Assert
            Assert.AreEqual(expectedAction, reported);
            Assert.AreEqual(0, outside, "An item is not a click outside the panel");
        }

        [Test]
        public void ReportAPressOnTheBackdropAsAClickOutside()
        {
            // Arrange
            AttachToPanel();
            var outside = 0;
            menu.OutsideClicked = () => outside++;
            VisualElement backdrop = menu.Q<VisualElement>("Backdrop");

            // Act
            using PointerDownEvent evt = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, button = 0, mousePosition = new Vector2(10f, 10f) });
            evt.target = backdrop;
            backdrop.SendEvent(evt);

            // Assert
            Assert.AreEqual(1, outside);
        }

        private void AttachToPanel()
        {
            documentGameObject = new GameObject(nameof(LobbyProfileMenuElementShould));
            var document = documentGameObject.AddComponent<UIDocument>();
            panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            document.panelSettings = panelSettings;
            document.rootVisualElement.Add(menu);
        }

        // A submit is reported as a click like a keyboard press; unlike a pointer click it needs no laid-out panel.
        private static void Submit(Button button)
        {
            using NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled();
            evt.target = button;
            button.SendEvent(evt);
        }
    }
}
