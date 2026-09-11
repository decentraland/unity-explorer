using DCL.Chat.ChatServices;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.FeatureFlags;
using DCL.Translation.Service;
using DCL.UI.InputFieldFormatting;
using NSubstitute;
using NUnit.Framework;
using System;
using UnityEngine;

namespace DCL.Tests.Editor
{
    /// <summary>
    ///     A chat message from a co-located peer is rendered by a label whose rich text is on —
    ///     <c>ChatEntryMessageBubbleElement</c> overrides the flag back on for the content element nested inside it —
    ///     so the peer's own markup has to be neutralized on the way in. The gate that decides this must key on
    ///     provenance, never on how the text looks: a message beginning with a status emoji used to be treated as a
    ///     copy of a system line and skipped the pipeline entirely, which let its sender choose (SEC-023).
    /// </summary>
    public class ChatHistoryServiceSanitizationShould
    {
        private static readonly ChatChannel.ChannelId CHANNEL = ChatChannel.NEARBY_CHANNEL_ID;

        private FakeChatMessagesBus bus = null!;
        private IChatHistory history = null!;
        private ChatHistoryService service = null!;

        [SetUp]
        public void SetUp()
        {
            // ChatMessage's constructor asks OfficialWalletsHelper whether the sender is official, and that helper
            // reads the feature-flag configuration in its own constructor, so both singletons are claimed and
            // released per test — the pairing ChatMessageReactionServiceShould uses.
            FeatureFlagsConfiguration.Reset();
            OfficialWalletsHelper.Reset();
            FeatureFlagsConfiguration.Initialize(new FeatureFlagsConfiguration(FeatureFlagsResultDto.Empty));
            OfficialWalletsHelper.Initialize(new OfficialWalletsHelper());

            bus = new FakeChatMessagesBus();
            history = Substitute.For<IChatHistory>();
            history.Channels.Returns(new System.Collections.Generic.Dictionary<ChatChannel.ChannelId, ChatChannel>());

            service = new ChatHistoryService(bus, history, new PassThroughFormatter(),
                ScriptableObject.CreateInstance<Chat.ChatConfig.ChatConfig>(),
                Substitute.For<ITranslationService>(),
                new CurrentChannelService());
        }

        [TearDown]
        public void TearDown()
        {
            service?.Dispose();
            OfficialWalletsHelper.Reset();
            FeatureFlagsConfiguration.Reset();
        }

        /// <summary>
        ///     Hand-written rather than substituted: raising an event through a generated proxy is what this test
        ///     needs on every case, and doing it via <c>Raise.Event</c> threw from inside NSubstitute's own handler.
        /// </summary>
        private sealed class FakeChatMessagesBus : IChatMessagesBus
        {
            public event Action<ChatChannel.ChannelId, ChatChannel.ChatChannelType, ChatMessage>? MessageAdded;

            public void Raise(ChatChannel.ChannelId channel, ChatChannel.ChatChannelType type, ChatMessage message) =>
                MessageAdded?.Invoke(channel, type, message);

            public void Send(ChatChannel channel, string message, ChatMessageOrigin origin, double timestamp) { }

            public void Dispose() { }
        }

        /// <summary>
        ///     Hand-written rather than substituted: <see cref="ITextFormatter.FormatText"/> takes a
        ///     <see cref="ReadOnlySpan{T}"/>, which NSubstitute cannot proxy. Returning the input unchanged also
        ///     keeps these assertions about the neutralization alone, not the formatter's own link markup.
        /// </summary>
        private sealed class PassThroughFormatter : ITextFormatter
        {
            public string FormatText(ReadOnlySpan<char> text) =>
                text.ToString();

            public void GetMatches(string text, System.Collections.Generic.List<(TextFormatMatchType, System.Text.RegularExpressions.Match)> matchesResult) { }
        }

        [TestCase("🟢 ", TestName = "green status marker")]
        [TestCase("🔴 ", TestName = "red status marker")]
        [TestCase("🟡 ", TestName = "yellow status marker")]
        [TestCase("", TestName = "no marker")]
        public void NeutralizeMarkupInAPeerMessageWhateverItStartsWith(string prefix)
        {
            // Arrange — the payload a peer would send to pass itself off as an official line carrying a link.
            string text = prefix + "<link=\"https://evil.example.com\">Verified ✔</link>";

            // Act
            RaiseIncoming(NonSystemMessage(text));

            // Assert — the marker must not buy the sender an exemption from the pipeline.
            string stored = LastStoredMessage();
            Assert.That(stored, Does.Not.Contain("<").And.Not.Contain(">"), stored);
            StringAssert.Contains("Verified", stored);
        }

        [Test]
        public void NeutralizeAnEscapeSequenceInAPeerMessage()
        {
            // Arrange — the content label has parseCtrlCharacters on, so TMP would decode this into a real tag.
            const string BACKSLASH = "\\";

            // Act
            RaiseIncoming(NonSystemMessage($"🟢 {BACKSLASH}u003Csize=400%{BACKSLASH}u003Ehuge"));

            // Assert
            Assert.That(LastStoredMessage(), Does.Not.Contain(BACKSLASH));
        }

        [Test]
        public void LeaveAGenuineSystemMessageAlone()
        {
            // Arrange — what the client itself emits for a teleport result: a status marker and no markup.
            var system = ChatMessage.NewFromSystem("🟢 Welcome to the genesis world!");

            // Act
            RaiseIncoming(system);

            // Assert — provenance, so it is stored verbatim and keeps the flag the feed styles it by.
            Assert.AreEqual("🟢 Welcome to the genesis world!", LastStoredMessage());
            Assert.IsTrue(LastStored().IsSystemMessage);
        }

        [Test]
        public void NotLetAPeerMessageInheritSystemProvenance()
        {
            // Act
            RaiseIncoming(NonSystemMessage("🟢 Your wallet needs verification"));

            // Assert — the feed picks the system prefab and colour off this flag, so it must stay false.
            Assert.IsFalse(LastStored().IsSystemMessage);
        }

        [Test]
        public void KeepOrdinaryPeerTextIntact()
        {
            // Act
            RaiseIncoming(NonSystemMessage("hey, 5 is < 10 right?"));

            // Assert — a bracket in ordinary prose is made inert, not deleted, and the words survive.
            string stored = LastStoredMessage();
            StringAssert.Contains("hey, 5 is", stored);
            StringAssert.Contains("10 right?", stored);
        }

        /// <summary>
        ///     A message that is not the client's own — the case the neutralization applies to. The gate under test
        ///     reads <c>IsSystemMessage</c> and nothing else, so <c>isSentByOwnUser</c> is set here only to skip the
        ///     audio feedback that runs afterwards: it reads player prefs, which have no backing store in a test.
        /// </summary>
        private static ChatMessage NonSystemMessage(string text) =>
            new (text, "Peer", "0x1234567890abcdef", true, "#cdef", 1.0);

        private void RaiseIncoming(ChatMessage message) =>
            bus.Raise(CHANNEL, ChatChannel.ChatChannelType.NEARBY, message);

        private ChatMessage LastStored()
        {
            foreach (var call in history.ReceivedCalls())
            {
                if (call.GetMethodInfo().Name == nameof(IChatHistory.AddMessage))
                    return (ChatMessage)call.GetArguments()[2];
            }

            Assert.Fail("no message was added to the history");
            return default;
        }

        private string LastStoredMessage() =>
            LastStored().Message;
    }
}
