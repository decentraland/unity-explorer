using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.BugReporting.UI;
using DCL.Clipboard;
using DCL.FeatureFlags;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Passport;
using DCL.Profiles;
using DCL.UI;
using DCL.UI.ProfileElements;
using DCL.UI.Profiles.Helpers;
using DCL.UI.SystemMenu;
using DCL.UserInAppInitializationFlow;
using DCL.Web3;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using MVC;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Utility.UIToolkit;

namespace DCL.Lobby.Tests
{
    [TestFixture]
    public class LobbyProfileMenuPresenterShould
    {
        private const string TEMPLATE_PATH = "Assets/DCL/Lobby/UIToolkit/LobbyProfileMenu.uxml";

        private IWeb3IdentityCache identityCache = null!;
        private IProfileRepository profileRepository = null!;
        private ISystemClipboard clipboard = null!;
        private IPassportBridge passportBridge = null!;
        private IMVCManager mvcManager = null!;
        private UnityAppWebBrowser webBrowser = null!;
        private ICompositeWeb3Provider web3Authenticator = null!;
        private IProfileCache profileCache = null!;
        private IUserInAppInitializationFlow initializationFlow = null!;
        private World world = null!;
        private LobbyProfileMenuElement menu = null!;
        private LobbyProfileMenuPresenter presenter = null!;
        private CancellationTokenSource cts = null!;
        private string address = null!;
        private Profile.CompactInfo profile;
        private GameObject documentGameObject = null!;
        private PanelSettings panelSettings = null!;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // The editor domain may still hold an initialized registry from a play-mode session.
            EcsTestsUtils.TearDownFeaturesRegistry();
            EcsTestsUtils.SetUpFeaturesRegistry(FeatureFlagsStrings.BUG_REPORT);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown() =>
            EcsTestsUtils.TearDownFeaturesRegistry();

        [SetUp]
        public void SetUp()
        {
            GetProfileThumbnailCommand.Reset();

            GetProfileThumbnailCommand.Initialize(new GetProfileThumbnailCommand(new ProfileRepositoryWrapper(Substitute.For<IProfileRepository>(),
                Substitute.For<IProfileCache>(), Substitute.For<ISpriteCache>(), Substitute.For<IWeb3IdentityCache>())));

            var identity = new IWeb3Identity.Random();
            address = identity.Address;
            identityCache = Substitute.For<IWeb3IdentityCache>();
            identityCache.Identity.Returns(identity);

            profileRepository = Substitute.For<IProfileRepository>();
            profile = new Profile.CompactInfo(UserId.New(address).Unwrap(), "Mojito", hasClaimedName: true);
            StubProfile(profile);

            clipboard = Substitute.For<ISystemClipboard>();
            passportBridge = Substitute.For<IPassportBridge>();
            passportBridge.ShowAsync(Arg.Any<PassportParams>()).Returns(UniTask.CompletedTask);
            mvcManager = Substitute.For<IMVCManager>();
            webBrowser = Substitute.For<UnityAppWebBrowser>(null!, null);
            web3Authenticator = Substitute.For<ICompositeWeb3Provider>();
            web3Authenticator.LogoutAsync(Arg.Any<CancellationToken>()).Returns(UniTask.CompletedTask);
            profileCache = Substitute.For<IProfileCache>();
            initializationFlow = Substitute.For<IUserInAppInitializationFlow>();
            initializationFlow.ExecuteAsync(Arg.Any<UserInAppInitializationFlowParameters>(), Arg.Any<CancellationToken>()).Returns(UniTask.CompletedTask);
            world = World.Create();

            menu = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TEMPLATE_PATH).InstantiateForElement<LobbyProfileMenuElement>();
            documentGameObject = new GameObject(nameof(LobbyProfileMenuPresenterShould));
            var document = documentGameObject.AddComponent<UIDocument>();
            panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            document.panelSettings = panelSettings;
            document.rootVisualElement.Add(menu);

            cts = new CancellationTokenSource();

            presenter = new LobbyProfileMenuPresenter(identityCache, profileRepository, clipboard, passportBridge, mvcManager, webBrowser,
                new UserLogout(identityCache, web3Authenticator, profileCache, initializationFlow, world, Entity.Null));
        }

        [TearDown]
        public void TearDown()
        {
            presenter.Dispose();
            cts.Cancel();
            cts.Dispose();
            world.Dispose();
            UnityEngine.Object.DestroyImmediate(documentGameObject);
            UnityEngine.Object.DestroyImmediate(panelSettings);
            GetProfileThumbnailCommand.Reset();
        }

        [Test]
        public void BindTheMenuClosedWithTheBugReportItem()
        {
            // Act
            presenter.Show(menu, cts.Token);

            // Assert
            Assert.IsFalse(menu.IsOpen);
            Assert.IsFalse(presenter.IsOpen);
            Assert.IsTrue(menu.CanReportBug);
        }

        [Test]
        public async Task ShowTheOwnProfileWhenOpened()
        {
            // Arrange
            presenter.Show(menu, cts.Token);

            // Act
            presenter.Toggle();
            await UniTask.Yield();

            // Assert
            Assert.IsTrue(presenter.IsOpen);
            Assert.AreEqual("Mojito", menu.UserName);
            Assert.AreEqual(profile.UserNameColor, menu.UserNameColor, "The name takes the color the profile computed for it");
            Assert.IsTrue(menu.IsVerified);
            Assert.AreEqual($"{address[..5]}...{address[^5..]}", menu.Address);
        }

        [Test]
        public async Task FallBackToGuestWhenTheProfileCannotBeFetched()
        {
            // Arrange: a first open succeeds, so the fallback has a previous profile to clear
            presenter.Show(menu, cts.Token);
            presenter.Open();
            await UniTask.Yield();
            presenter.Dismiss();

            profileRepository.GetAsync(Arg.Any<string>(), 0, Arg.Any<URLDomain?>(), Arg.Any<CancellationToken>(), true,
                                  IProfileRepository.FetchBehaviour.Default, ProfileTier.Kind.Compact, Arg.Any<IPartitionComponent?>())
                             .Returns(UniTask.FromException<ProfileTier?>(new InvalidOperationException("profiles endpoint down")));

            LogAssert.Expect(LogType.Exception, "InvalidOperationException: profiles endpoint down");

            // Act
            presenter.Open();
            await UniTask.Yield();

            // Assert
            Assert.AreEqual("Guest", menu.UserName);
            Assert.AreEqual(Color.white, menu.UserNameColor, "The previous profile's color is not kept");
            Assert.IsFalse(menu.IsVerified);
            Assert.AreEqual($"{address[..5]}...{address[^5..]}", menu.Address, "The address comes from the identity, not from the profile");
        }

        [Test]
        public void ShowNothingToCopyWithoutAnIdentity()
        {
            // Arrange
            identityCache.Identity.Returns((IWeb3Identity?)null);
            presenter.Show(menu, cts.Token);

            // Act
            presenter.Open();
            Submit("CopyAddress");
            Submit("ProfileItem");

            // Assert
            Assert.AreEqual("Guest", menu.UserName);
            clipboard.DidNotReceive().Set(Arg.Any<string>());
            passportBridge.DidNotReceive().ShowAsync(Arg.Any<PassportParams>());
        }

        [Test]
        public async Task CopyTheFullAddressAndShowTheConfirmation()
        {
            // Arrange
            presenter.Show(menu, cts.Token);
            presenter.Open();
            await UniTask.Yield();

            // Act
            Submit("CopyAddress");

            // Assert
            clipboard.Received(1).Set(address);
            Assert.IsTrue(menu.IsCopiedShown);
            Assert.IsTrue(presenter.IsOpen, "Copying keeps the menu open");
        }

        [Test]
        public async Task DropTheCopiedConfirmationWhenDismissed()
        {
            // Arrange
            presenter.Show(menu, cts.Token);
            presenter.Open();
            await UniTask.Yield();
            Submit("CopyAddress");

            // Act
            presenter.Dismiss();

            // Assert
            Assert.IsFalse(menu.IsCopiedShown);
            Assert.IsFalse(presenter.IsOpen);
        }

        [Test]
        public void CloseOnAClickOutside()
        {
            // Arrange
            presenter.Show(menu, cts.Token);
            presenter.Open();

            // Act
            menu.OutsideClicked!.Invoke();

            // Assert
            Assert.IsFalse(presenter.IsOpen);
        }

        [Test]
        public async Task OpenTheOwnPassportAndClose()
        {
            // Arrange
            presenter.Show(menu, cts.Token);
            presenter.Open();
            await UniTask.Yield();

            // Act
            Submit("ProfileItem");

            // Assert
            Assert.IsFalse(presenter.IsOpen);
            await passportBridge.Received(1).ShowAsync(Arg.Is<PassportParams>(p => p.UserId == address && p.IsOwnProfile));
        }

        [Test]
        public void RequestTheSettingsAndClose()
        {
            // Arrange
            var requested = 0;
            presenter.SettingsRequested = () => requested++;
            presenter.Show(menu, cts.Token);
            presenter.Open();

            // Act
            Submit("SettingsItem");

            // Assert
            Assert.IsFalse(presenter.IsOpen);
            Assert.AreEqual(1, requested);
        }

        [Test]
        public void OpenTheBugReportAndClose()
        {
            // Arrange
            presenter.Show(menu, cts.Token);
            presenter.Open();

            // Act
            Submit("ReportBugItem");

            // Assert
            Assert.IsFalse(presenter.IsOpen);
            mvcManager.Received(1).ShowAsync(Arg.Any<ShowCommand<BugReportView, BugReportParams>>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task SignOutAndClose()
        {
            // Arrange
            presenter.Show(menu, cts.Token);
            presenter.Open();

            // Act
            Submit("SignOutItem");
            await UniTask.Yield();

            // Assert
            Assert.IsFalse(presenter.IsOpen);
            await web3Authenticator.Received(1).LogoutAsync(Arg.Any<CancellationToken>());
            profileCache.Received(1).Remove(new Web3Address(address));
            await initializationFlow.Received(1).ExecuteAsync(Arg.Is<UserInAppInitializationFlowParameters>(p => p.LoadSource == IUserInAppInitializationFlow.LoadSource.Logout), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task KeepSigningOutAfterTheLobbyHides()
        {
            // Arrange
            var logoutStarted = new UniTaskCompletionSource();
            web3Authenticator.LogoutAsync(Arg.Any<CancellationToken>()).Returns(logoutStarted.Task);
            presenter.Show(menu, cts.Token);
            presenter.Open();
            Submit("SignOutItem");

            // Act
            presenter.Hide();
            cts.Cancel();
            logoutStarted.TrySetResult();
            await UniTask.Yield();

            // Assert
            await initializationFlow.Received(1).ExecuteAsync(Arg.Any<UserInAppInitializationFlowParameters>(), Arg.Any<CancellationToken>());
        }

        [TestCase("TermsLink", DecentralandUrl.TermsOfUse)]
        [TestCase("PrivacyLink", DecentralandUrl.PrivacyPolicy)]
        public void OpenTheLegalPagesAndClose(string buttonName, DecentralandUrl url)
        {
            // Arrange
            presenter.Show(menu, cts.Token);
            presenter.Open();

            // Act
            Submit(buttonName);

            // Assert
            Assert.IsFalse(presenter.IsOpen);
            webBrowser.Received(1).OpenUrlMainThreadOnly(url);
        }

        [Test]
        public void LeaveTheMenuAloneOnceHidden()
        {
            // Arrange
            presenter.Show(menu, cts.Token);
            presenter.Open();

            // Act
            presenter.Hide();
            presenter.Dismiss();
            presenter.Toggle();
            Submit("SettingsItem");

            // Assert
            Assert.IsFalse(menu.IsOpen);
            Assert.IsNull(menu.SettingsClicked);
        }

        private void StubProfile(Profile.CompactInfo compact) =>
            profileRepository.GetAsync(Arg.Any<string>(), 0, Arg.Any<URLDomain?>(), Arg.Any<CancellationToken>(), true,
                                  IProfileRepository.FetchBehaviour.Default, ProfileTier.Kind.Compact, Arg.Any<IPartitionComponent?>())
                             .Returns(UniTask.FromResult<ProfileTier?>(compact));

        private void Submit(string buttonName)
        {
            Button button = menu.Q<Button>(buttonName);
            using NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled();
            evt.target = button;
            button.SendEvent(evt);
        }
    }
}
