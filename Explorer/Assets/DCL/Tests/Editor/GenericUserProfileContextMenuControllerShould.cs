using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Chat;
using DCL.FeatureFlags;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connectivity;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.UI;
using DCL.VoiceChat;
using DCL.VoiceChat.Nearby;
using DCL.VoiceChat.Nearby.MutePersistence;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using ECS.SceneLifeCycle.Realm;
using ECS.TestSuite;
using MVC;
using NSubstitute;
using NUnit.Framework;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Avatar = DCL.Profiles.Avatar;

namespace DCL.Tests.Editor
{
    [TestFixture]
    public class GenericUserProfileContextMenuControllerShould
    {
        private const string SETTINGS_PATH = "Assets/DCL/UI/GenericContextMenu/Prefabs/GenericUserProfileContextMenuSettings.asset";
        private const string TARGET_USER_ID = "0x2222222222222222222222222222222222222222";

        private GenericUserProfileContextMenuController controller = null!;
        private DefaultProfileCache profileCache = null!;
        private IWeb3IdentityCache identityCache = null!;
        private Profile? targetProfile;

        [SetUp]
        public void SetUp()
        {
            // VoiceChat resolves to Friends && FriendsUserBlocking && isEditor, so the blocking flag is what turns the call button on
            EcsTestsUtils.SetUpFeaturesRegistry(FeatureFlagsStrings.FRIENDS_USER_BLOCKING);

            var settings = AssetDatabase.LoadAssetAtPath<GenericUserProfileContextMenuSettings>(SETTINGS_PATH);
            Assert.IsNotNull(settings, $"Could not load the context menu settings from {SETTINGS_PATH}");

            profileCache = new DefaultProfileCache();
            identityCache = Substitute.For<IWeb3IdentityCache>();

            controller = new GenericUserProfileContextMenuController(
                null,
                new ChatEventBus(),
                Substitute.For<IMVCManager>(),
                settings,
                Substitute.For<IAnalyticsController>(),
                Substitute.For<IOnlineUsersProvider>(),
                Substitute.For<IRealmNavigator>(),
                null,
                false,
                null!,
                Substitute.For<IVoiceChatOrchestratorActions>(),
                new UnityAppWebBrowser(Substitute.For<IDecentralandUrlsSource>()),
                Substitute.For<IDecentralandUrlsSource>(),
                Substitute.For<ISelfProfile>(),
                profileCache,
                identityCache,
                new NearbyMuteService(Substitute.For<INearbyMuteCache>(), Substitute.For<INearbyMuteRepository>()));
        }

        [TearDown]
        public void TearDown()
        {
            targetProfile?.Dispose();

            EcsTestsUtils.TearDownFeaturesRegistry();
        }

        [TestCase(false, false, true)]
        [TestCase(true, false, false)]
        [TestCase(false, true, false)]
        [TestCase(true, true, false)]
        public async Task EnableTheCallButtonOnlyWhenNeitherParticipantIsGuest(bool callerIsGuest, bool calleeIsGuest, bool expectedEnabled)
        {
            // Arrange
            SetUpIdentity(callerIsGuest ? LoginMethod.GUEST : LoginMethod.METAMASK);
            targetProfile = CacheProfile(TARGET_USER_ID, "callee", calleeIsGuest);

            // Act
            await ShowContextMenuAsync();

            // Assert
            Assert.AreEqual(expectedEnabled, CallButton().Enabled);
        }

        [Test]
        public async Task EnableTheCallButtonWhenThereIsNoIdentityYet()
        {
            // Arrange
            targetProfile = CacheProfile(TARGET_USER_ID, "callee", false);

            // Act
            await ShowContextMenuAsync();

            // Assert
            Assert.IsTrue(CallButton().Enabled);
        }

        private void SetUpIdentity(LoginMethod loginMethod)
        {
            var identity = Substitute.For<IWeb3Identity>();
            identity.Method.Returns(loginMethod);
            identityCache.Identity.Returns(identity);
        }

        private UniTask ShowContextMenuAsync() =>
            controller.ShowUserProfileContextMenuAsync(
                new Profile.CompactInfo(UserId.New(TARGET_USER_ID).Unwrap(), "callee"),
                Vector3.zero,
                Vector2.zero,
                CancellationToken.None,
                UniTask.CompletedTask);

        private Profile CacheProfile(string userId, string name, bool isGuest)
        {
            var profile = new Profile(UserId.New(userId).Unwrap(), name, new Avatar());
            profile.HasConnectedWeb3 = !isGuest;
            profileCache.Set(userId, profile);

            return profile;
        }

        private GenericContextMenuElement CallButton() =>
            (GenericContextMenuElement)typeof(GenericUserProfileContextMenuController)
                                      .GetField("contextMenuCallButton", BindingFlags.NonPublic | BindingFlags.Instance)!
                                      .GetValue(controller)!;
    }
}
