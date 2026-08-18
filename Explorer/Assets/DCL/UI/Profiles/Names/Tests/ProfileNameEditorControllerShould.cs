using System.Linq;
using System.Reflection;
using DCL.Browser;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Profiles;
using DCL.Profiles.Self;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DCL.UI.ProfileNames.Tests
{
    /// <summary>
    ///     Regression coverage for https://github.com/decentraland/unity-explorer/issues/9550: a minted
    ///     Name cannot be equipped when it is spelled the same as the current non-unique display name.
    /// </summary>
    [TestFixture]
    public class ProfileNameEditorControllerShould
    {
        private const string PREFAB_PATH = "Assets/DCL/UI/Profiles/Names/Assets/ProfileNameEditor.prefab";
        private const string OWNED_NAME = "test2000";

        private GameObject viewGameObject;
        private ProfileNameEditorView view;
        private ProfileNameEditorController controller;
        private Profile profile;

        [SetUp]
        public void SetUp()
        {
            EcsTestsUtils.SetUpFeaturesRegistry();

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
            Assert.IsNotNull(prefab, $"Could not load ProfileNameEditor prefab from {PREFAB_PATH}");

            viewGameObject = Object.Instantiate(prefab);
            view = viewGameObject.GetComponent<ProfileNameEditorView>();
            Assert.IsNotNull(view, "ProfileNameEditorView component not found on the instantiated prefab");

            controller = new ProfileNameEditorController(
                () => view,
                new UnityAppWebBrowser(Substitute.For<IDecentralandUrlsSource>()),
                Substitute.For<ISelfProfile>(),
                Substitute.For<INftNamesProvider>(),
                Substitute.For<IDecentralandUrlsSource>(),
                new ProfileChangesBus());
        }

        [TearDown]
        public void TearDown()
        {
            profile?.Dispose();

            if (viewGameObject != null)
                Object.DestroyImmediate(viewGameObject);

            EcsTestsUtils.TearDownFeaturesRegistry();
        }

        [Test]
        public void NotPreSelectOwnedNameDropdownEntryWhenDisplayNameMatchesButIsNotClaimed()
        {
            // Arrange: the user's *non-unique* display name happens to be spelled the same as an
            // owned NFT name they haven't equipped yet (HasClaimedName == false) - the #9550 repro.
            profile = new Profile(UserId.New("0x1234567890123456789012345678901234abcd").Unwrap(), OWNED_NAME, new DCL.Profiles.Avatar());
            profile.HasClaimedName = false;

            using INftNamesProvider.PaginatedNamesResponse names = new (1, new[] { OWNED_NAME });

            ProfileNameEditorView.ClaimedNameConfig config = view.ClaimedNameContainer;

            // Act: drive the same private setup routine the popup runs every time it's shown.
            InvokeSetUpClaimed(config, profile, names);

            // Assert: the dropdown must stay unselected (-1, placeholder) so the matching entry
            // remains clickable and still fires onValueChanged. Pre-fix, FindIndex matches on the
            // name string alone and pre-selects index 0; re-clicking the already-selected entry
            // never fires TMP_Dropdown.onValueChanged, so Save stays disabled forever.
            Assert.AreEqual(-1, config.claimedNameDropdown.value,
                "the claimed-name dropdown must not pre-select an owned name that merely matches the " +
                "current (unclaimed) display name, or Save can never be enabled (#9550)");
        }

        private void InvokeSetUpClaimed(ProfileNameEditorView.ClaimedNameConfig config, Profile profile, INftNamesProvider.PaginatedNamesResponse names)
        {
            // SetUpClaimed is a local function nested in OnBeforeViewShow/SetUpAsync; the compiler
            // emits it as a private instance method on the controller (it closes over the
            // `dropdownOptions` field), so it's reached via reflection instead of a direct call.
            MethodInfo setUpClaimed = typeof(ProfileNameEditorController)
                .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                .Single(m => m.Name.Contains("SetUpClaimed"));

            setUpClaimed.Invoke(controller, new object[] { config, profile, names });
        }
    }
}
