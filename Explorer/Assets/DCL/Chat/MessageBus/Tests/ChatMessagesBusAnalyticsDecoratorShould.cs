using DCL.Chat.History;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.Profiles;
using ECS.TestSuite;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;

namespace DCL.Chat.MessageBus.Tests
{
    /// <summary>
    ///     Regression coverage for the "mentions" field of the <see cref="AnalyticsEvents.Ui.MESSAGE_SENT" /> event:
    ///     it must carry wallet-address strings (JArray.Add takes object, so <see cref="UserId" />'s implicit
    ///     string conversion never applies), and each tracked payload must own its mentions array so
    ///     later sends cannot mutate events already queued for flush.
    /// </summary>
    [TestFixture]
    public class ChatMessagesBusAnalyticsDecoratorShould
    {
        private const string FIRST_WALLET = "0x1111111111111111111111111111111111111111";
        private const string SECOND_WALLET = "0x2222222222222222222222222222222222222222";

        private IChatMessagesBus core = null!;
        private IAnalyticsController analytics = null!;
        private IProfileCache profileCache = null!;
        private ChatMessagesBusAnalyticsDecorator decorator = null!;
        private List<JObject> trackedPayloads = null!;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // Profile.CompactInfo touches FeaturesRegistry.Instance on construction; the editor
            // domain may still hold an initialized registry from a play-mode session
            EcsTestsUtils.TearDownFeaturesRegistry();
            EcsTestsUtils.SetUpFeaturesRegistry();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown() =>
            EcsTestsUtils.TearDownFeaturesRegistry();

        [SetUp]
        public void SetUp()
        {
            core = Substitute.For<IChatMessagesBus>();
            analytics = Substitute.For<IAnalyticsController>();
            profileCache = Substitute.For<IProfileCache>();
            trackedPayloads = new List<JObject>();

            analytics.When(a => a.Track(AnalyticsEvents.Ui.MESSAGE_SENT, Arg.Any<JObject?>(), Arg.Any<bool>()))
                     .Do(call => trackedPayloads.Add((JObject)call[1]!));

            // selfProfile is only dereferenced when timestamp > 0; every Send here passes 0
            decorator = new ChatMessagesBusAnalyticsDecorator(core, analytics, profileCache, null!);
        }

        [TearDown]
        public void TearDown() =>
            decorator.Dispose();

        private static ProfileTier? CachedProfile(string wallet, string name) =>
            (ProfileTier?)new Profile.CompactInfo(UserId.New(wallet).Unwrap(), name);

        [Test]
        public void TrackMentionOfCachedProfileAsWalletAddressString()
        {
            profileCache.GetByUserName("TestName").Returns(CachedProfile(FIRST_WALLET, "TestName"));

            Assert.DoesNotThrow(() => decorator.Send(ChatChannel.NEARBY_CHANNEL, "hello @TestName", ChatMessageOrigin.Chat, 0));

            core.Received(1).Send(ChatChannel.NEARBY_CHANNEL, "hello @TestName", ChatMessageOrigin.Chat, 0);
            Assert.That(trackedPayloads, Has.Count.EqualTo(1));

            JObject payload = trackedPayloads[0];
            Assert.That(payload["is_mention"]!.Value<bool>(), Is.True);

            var mentions = (JArray)payload["mentions"]!;
            Assert.That(mentions, Has.Count.EqualTo(1));
            Assert.That(mentions[0].Type, Is.EqualTo(JTokenType.String));
            Assert.That(mentions[0].Value<string>(), Is.EqualTo(FIRST_WALLET));
        }

        [Test]
        public void KeepMentionsOfQueuedEventsIntactAcrossSends()
        {
            profileCache.GetByUserName("FirstUser").Returns(CachedProfile(FIRST_WALLET, "FirstUser"));
            profileCache.GetByUserName("SecondUser").Returns(CachedProfile(SECOND_WALLET, "SecondUser"));

            Assert.DoesNotThrow(() =>
            {
                decorator.Send(ChatChannel.NEARBY_CHANNEL, "hi @FirstUser", ChatMessageOrigin.Chat, 0);
                decorator.Send(ChatChannel.NEARBY_CHANNEL, "hi @SecondUser", ChatMessageOrigin.Chat, 0);
            });

            Assert.That(trackedPayloads, Has.Count.EqualTo(2));

            // The analytics queue holds payload references until flush: the first event's mentions
            // must survive the second send unchanged
            var firstMentions = (JArray)trackedPayloads[0]["mentions"]!;
            Assert.That(firstMentions, Has.Count.EqualTo(1));
            Assert.That(firstMentions[0].Value<string>(), Is.EqualTo(FIRST_WALLET));

            var secondMentions = (JArray)trackedPayloads[1]["mentions"]!;
            Assert.That(secondMentions, Has.Count.EqualTo(1));
            Assert.That(secondMentions[0].Value<string>(), Is.EqualTo(SECOND_WALLET));
        }
    }
}
