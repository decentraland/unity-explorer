using DCL.Communities.CommunitiesCard.Announcements;
using DCL.Communities.CommunitiesCard.Members;
using DCL.Notifications.NotificationEntry;
using DCL.NotificationsBus.NotificationTypes;
using DCL.Profiles;
using ECS.TestSuite;
using NUnit.Framework;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace DCL.Tests.Editor
{
    /// <summary>
    ///     A display name and an announcement body are strings other users author, and no backend rejects the ones
    ///     that happen to look like TMP markup. They reach the friends notification toast, the community
    ///     announcement card and the community member list, so each of those sinks is what keeps them inert and
    ///     bounded (SEC-050).
    /// </summary>
    /// <remarks>
    ///     The toast is asserted end to end because its title label has to stay rich text — its own copy is
    ///     <c>&lt;color&gt;</c> markup — which leaves escaping as the only defence. The community labels render
    ///     nothing but the untrusted value, so rich text is off on them instead, and what a test can protect there
    ///     is the prefab flag: a prefab edit could re-enable it unnoticed.
    /// </remarks>
    public class NotificationAndCommunityTextShould
    {
        private const string FRIENDS_NOTIFICATION_PREFAB_PATH = "Assets/DCL/Notifications/Assets/FriendsNotification.prefab";
        private const string ANNOUNCEMENT_CARD_PREFAB_PATH = "Assets/DCL/Communities/CommunitiesCard/Prefabs/AnnouncementCard.prefab";
        private const string COMMUNITY_MEMBER_PREFAB_PATH = "Assets/DCL/Communities/CommunitiesCard/Prefabs/CommunityMember.prefab";
        private const string REQUESTS_RECEIVED_MEMBER_PREFAB_PATH = "Assets/DCL/Communities/CommunitiesBrowser/Prefabs/RequestsReceived_MemberCard.prefab";

        private const string SENDER_ADDRESS = "0x79fdd6f8ba257bda1d5a2a413ae0b43ec300ed10";
        private const string SENDER_SUFFIX = "ed10";

        // "<color=#{0}>{1} <color=#ECEBED>{2}" and its unclaimed variant, which adds a third tag for the suffix.
        private const int CLAIMED_TEMPLATE_TAGS = 2;
        private const int UNCLAIMED_TEMPLATE_TAGS = 3;

        private GameObject canvasRoot = null!;
        private FriendsNotificationView notificationView = null!;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // Building a CompactInfo derives the validated name, which reads the features registry. The editor
            // domain may still hold one initialized by a play-mode session.
            EcsTestsUtils.TearDownFeaturesRegistry();
            EcsTestsUtils.SetUpFeaturesRegistry();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown() =>
            EcsTestsUtils.TearDownFeaturesRegistry();

        [SetUp]
        public void SetUp()
        {
            // A copy of the shipped toast under a canvas, so the title label is the one players read and the asset
            // itself is never written to.
            canvasRoot = new GameObject(nameof(NotificationAndCommunityTextShould), typeof(Canvas));

            notificationView = Object.Instantiate(LoadPrefab(FRIENDS_NOTIFICATION_PREFAB_PATH), canvasRoot.transform)
                                     .GetComponentInChildren<FriendsNotificationView>(true);
        }

        [TearDown]
        public void TearDown() =>
            Object.DestroyImmediate(canvasRoot);

        [TestCase("<size=400%><link=\"https://evil.example.com\">Verified")]
        [TestCase("<color=#00FF00>Verified Admin</color>")]
        [TestCase("<b><i><u>nested")]
        public void NotLetASenderNameOpenATagInTheNotificationTitle(string senderName)
        {
            // Act
            notificationView.ConfigureFromReceivedNotificationData(FriendRequestFrom(senderName, hasClaimedName: true));

            // Assert — the title carries exactly the brackets its own template contributes and no more, so whatever
            // the sender wrote is text rather than markup, however it was crafted.
            string title = notificationView.TitleText.text;
            Assert.AreEqual(CLAIMED_TEMPLATE_TAGS, CountOf(title, '<'), $"opening brackets in \"{title}\"");
            Assert.AreEqual(CLAIMED_TEMPLATE_TAGS, CountOf(title, '>'), $"closing brackets in \"{title}\"");
        }

        [Test]
        public void KeepTheNotificationTitleTemplateMarkupWorking()
        {
            // Arrange
            FriendRequestReceivedNotification notification = FriendRequestFrom("<size=400%>Verified", hasClaimedName: true);

            // Act
            notificationView.ConfigureFromReceivedNotificationData(notification);

            // Assert — escaping the name must not cost the template the two colours it needs: one for the name, one
            // for the sentence after it. The name itself stays readable, it is made inert rather than censored.
            string title = notificationView.TitleText.text;
            StringAssert.StartsWith("<color=#", title);
            StringAssert.Contains("<color=#ECEBED>", title);
            StringAssert.Contains(notification.GetTitle(), title);
            StringAssert.Contains("Verified", title);
            StringAssert.Contains("size=400%", title);
        }

        [Test]
        public void NotLetAnUnclaimedSenderNameOpenATagInTheNotificationTitle()
        {
            // Act
            notificationView.ConfigureFromReceivedNotificationData(FriendRequestFrom("<size=400%>Verified", hasClaimedName: false));

            // Assert — the unclaimed template adds a third tag for the wallet suffix, which still has to render.
            string title = notificationView.TitleText.text;
            Assert.AreEqual(UNCLAIMED_TEMPLATE_TAGS, CountOf(title, '<'), $"opening brackets in \"{title}\"");
            Assert.AreEqual(UNCLAIMED_TEMPLATE_TAGS, CountOf(title, '>'), $"closing brackets in \"{title}\"");
            StringAssert.Contains($"<color=#A09BA8>#{SENDER_SUFFIX}", title);
        }

        [Test]
        public void CapAnOversizedSenderNameInTheNotificationTitle()
        {
            // Arrange — two names well past any plausible cap. The toast grows to fit its title, so an uncapped name
            // is an unbounded layout pass.
            var longName = new string('a', 4_000);
            var longerName = new string('a', 40_000);

            // Act
            notificationView.ConfigureFromReceivedNotificationData(FriendRequestFrom(longName, hasClaimedName: true));
            int renderedLongLength = notificationView.TitleText.text.Length;
            notificationView.ConfigureFromReceivedNotificationData(FriendRequestFrom(longerName, hasClaimedName: true));

            // Assert — the rendered length saturates instead of tracking the input, whatever the exact cap is.
            Assert.Less(renderedLongLength, longName.Length);
            Assert.AreEqual(renderedLongLength, notificationView.TitleText.text.Length);
            StringAssert.Contains("…", notificationView.TitleText.text);
        }

        [TestCase("Guybrush")]
        [TestCase("O'Brien \"Mo\"")]
        [TestCase("Élodie del Río")]
        public void RenderAnOrdinarySenderNameUnchanged(string senderName)
        {
            // Act
            notificationView.ConfigureFromReceivedNotificationData(FriendRequestFrom(senderName, hasClaimedName: true));

            // Assert — apostrophes and straight quotes are ordinary punctuation in a name, not markup: hardening the
            // sink must not clip them, swap them for lookalikes, or append an ellipsis to a name that fits.
            string title = notificationView.TitleText.text;
            StringAssert.Contains(senderName, title);
            Assert.That(title, Does.Not.Contain("…"));
            Assert.AreEqual(CLAIMED_TEMPLATE_TAGS, CountOf(title, '<'), $"opening brackets in \"{title}\"");
        }

        [Test]
        public void PreferTheFilteredProfileNameForATipSender()
        {
            // Arrange — unlike a friend request, a tip carries a full profile, whose ValidatedName is the name with
            // everything but letters and digits already removed.
            var notification = new TipReceivedNotification
            {
                SenderProfile = new Profile.CompactInfo(SENDER_ADDRESS, "<size=400%><link=\"https://evil\">Verified", hasClaimedName: true),
                Metadata = new TipReceivedNotificationMetadata { TipAmount = 10 },
            };

            // Act
            notificationView.ConfigureFromTipReceivedNotificationData(notification);

            // Assert — none of the crafted fragments reach the label, and the readable part of the name still does.
            string title = notificationView.TitleText.text;
            Assert.That(title, Does.Not.Contain("size=").And.Not.Contain("link=").And.Not.Contain("://"));
            StringAssert.Contains("Verified", title);
        }

        [Test]
        public void RenderAnAnnouncementBodyAsPlainText()
        {
            // Arrange — the shipped asset rather than an instance, so this is an assertion about what players get.
            var asset = LoadPrefab(ANNOUNCEMENT_CARD_PREFAB_PATH).GetComponentInChildren<AnnouncementCardView>(true);

            // Assert — the label carries nothing but the body another user wrote, never styled copy of its own, so
            // rich text is off and the body renders literally. Guarded here because a prefab edit could re-enable it.
            Assert.IsFalse(LabelOf(asset, "announcementContent").richText, "announcementContent");
        }

        [TestCase(COMMUNITY_MEMBER_PREFAB_PATH)]
        [TestCase(REQUESTS_RECEIVED_MEMBER_PREFAB_PATH)]
        public void RenderACommunityMemberNameAsPlainText(string prefabPath)
        {
            // Arrange
            var asset = LoadPrefab(prefabPath).GetComponentInChildren<MemberListItemView>(true);

            // Assert — the label carries nothing but a member's name, and its colour is applied as a property rather
            // than as markup, so nothing here needs rich text.
            Assert.IsFalse(LabelOf(asset, "userName").richText, prefabPath);
        }

        private static FriendRequestReceivedNotification FriendRequestFrom(string senderName, bool hasClaimedName) =>
            new ()
            {
                Metadata = new FriendRequestReceivedNotificationMetadata
                {
                    Sender = new FriendRequestProfile
                    {
                        Address = SENDER_ADDRESS,
                        Name = senderName,
                        HasClaimedName = hasClaimedName,
                        ProfileImageUrl = string.Empty,
                    },
                },
            };

        private static GameObject LoadPrefab(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.IsNotNull(prefab, $"No prefab at {prefabPath}.");
            return prefab;
        }

        /// <summary>
        ///     Resolves the label through the serialized member the view assigns to, so an assertion cannot drift onto
        ///     a different label than the one under test.
        /// </summary>
        private static TMP_Text LabelOf(Component target, string memberName)
        {
            const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.NonPublic;

            var type = target.GetType();

            object? member = type.GetField(memberName, FLAGS)?.GetValue(target)
                             ?? type.GetProperty(memberName, FLAGS)?.GetValue(target);

            if (member is not TMP_Text label)
                throw new AssertionException($"{type.Name}.{memberName} is not a serialized TMP_Text.");

            return label;
        }

        private static int CountOf(string value, char character)
        {
            var count = 0;

            foreach (char current in value)
            {
                if (current == character)
                    count++;
            }

            return count;
        }
    }
}
