using System.Reflection;
using DCL.Chat.ChatReactions.Core;
using DCL.Prefs;
using NUnit.Framework;

namespace DCL.Chat.ChatReactions.Tests
{
    [TestFixture]
    public class ChatReactionRecentsServiceShould
    {
        private static readonly int[] FIXED_DEFAULTS = { 0, 1, 2 };
        private const int MAX_RECENT = 3;

        [SetUp]
        public void SetUp()
        {
            // Inject InMemoryDCLPlayerPrefs via reflection (established test pattern)
            var field = typeof(DCLPlayerPrefs).GetField("dclPrefs",
                BindingFlags.NonPublic | BindingFlags.Static);

            field!.SetValue(null, new InMemoryDCLPlayerPrefs());
        }

        [TearDown]
        public void TearDown()
        {
            DCLPlayerPrefs.DeleteKey(DCLPrefKeys.CHAT_REACTION_FAVORITES);

            // Reset static field so other tests aren't affected
            var field = typeof(DCLPlayerPrefs).GetField("dclPrefs",
                BindingFlags.NonPublic | BindingFlags.Static);

            field!.SetValue(null, null);
        }

        [Test]
        public void ReturnEmptyRecentsInitially()
        {
            var service = new ChatReactionRecentsService(FIXED_DEFAULTS, MAX_RECENT);
            Assert.That(service.Recents.Count, Is.EqualTo(0));
        }

        // Emojis already in the fixed defaults row should not appear in the recents list.
        [Test]
        public void IgnoreFixedDefaults()
        {
            // Arrange
            var service = new ChatReactionRecentsService(FIXED_DEFAULTS, MAX_RECENT);

            // Act
            service.RecordUsage(0); // fixed default
            service.RecordUsage(1); // fixed default

            // Assert
            Assert.That(service.Recents.Count, Is.EqualTo(0));
        }

        [Test]
        public void TrackNonDefaultEmoji()
        {
            var service = new ChatReactionRecentsService(FIXED_DEFAULTS, MAX_RECENT);
            service.RecordUsage(10);
            Assert.That(service.Recents.Count, Is.EqualTo(1));
            Assert.That(service.Recents[0], Is.EqualTo(10));
        }

        [Test]
        public void PlaceMostRecentFirst()
        {
            // Arrange
            var service = new ChatReactionRecentsService(FIXED_DEFAULTS, MAX_RECENT);

            // Act
            service.RecordUsage(10);
            service.RecordUsage(20);
            service.RecordUsage(30);

            // Assert
            Assert.That(service.Recents[0], Is.EqualTo(30)); // most recent
            Assert.That(service.Recents[1], Is.EqualTo(20));
            Assert.That(service.Recents[2], Is.EqualTo(10)); // oldest
        }

        [Test]
        public void MoveToFrontOnReuse()
        {
            // Arrange
            var service = new ChatReactionRecentsService(FIXED_DEFAULTS, MAX_RECENT);

            service.RecordUsage(10);
            service.RecordUsage(20);
            service.RecordUsage(30);

            // Act — re-use 10, it should jump to front
            service.RecordUsage(10);

            // Assert
            Assert.That(service.Recents[0], Is.EqualTo(10));
            Assert.That(service.Recents[1], Is.EqualTo(30));
            Assert.That(service.Recents[2], Is.EqualTo(20));
            Assert.That(service.Recents.Count, Is.EqualTo(MAX_RECENT));
        }

        [Test]
        public void DropOldestWhenCapacityExceeded()
        {
            // Arrange
            var service = new ChatReactionRecentsService(FIXED_DEFAULTS, MAX_RECENT);

            service.RecordUsage(10);
            service.RecordUsage(20);
            service.RecordUsage(30);

            // Act — 10 should be evicted
            service.RecordUsage(40);

            // Assert
            Assert.That(service.Recents.Count, Is.EqualTo(MAX_RECENT));
            Assert.That(service.Recents[0], Is.EqualTo(40));
            Assert.That(service.Recents[1], Is.EqualTo(30));
            Assert.That(service.Recents[2], Is.EqualTo(20));
        }

        // A second instance created before flush should see nothing, verifying save is deferred.
        [Test]
        public void NotPersistUntilFlushed()
        {
            // Arrange
            var service = new ChatReactionRecentsService(FIXED_DEFAULTS, MAX_RECENT);
            service.RecordUsage(10);
            service.RecordUsage(20);

            // Act
            var service2 = new ChatReactionRecentsService(FIXED_DEFAULTS, MAX_RECENT);

            // Assert
            Assert.That(service2.Recents.Count, Is.EqualTo(0));
        }

        [Test]
        public void PersistAfterFlush()
        {
            // Arrange
            var service = new ChatReactionRecentsService(FIXED_DEFAULTS, MAX_RECENT);
            service.RecordUsage(10);
            service.RecordUsage(20);

            // Act
            service.FlushIfDirty();

            // Assert — new instance should see the persisted data in recency order
            var service2 = new ChatReactionRecentsService(FIXED_DEFAULTS, MAX_RECENT);
            Assert.That(service2.Recents.Count, Is.EqualTo(2));
            Assert.That(service2.Recents[0], Is.EqualTo(20)); // most recent
            Assert.That(service2.Recents[1], Is.EqualTo(10));
        }

        [Test]
        public void FlushIsNoOpWhenClean()
        {
            // Arrange
            var service = new ChatReactionRecentsService(FIXED_DEFAULTS, MAX_RECENT);

            // Act — no RecordUsage calls, nothing dirty
            service.FlushIfDirty();

            // Assert
            var service2 = new ChatReactionRecentsService(FIXED_DEFAULTS, MAX_RECENT);
            Assert.That(service2.Recents.Count, Is.EqualTo(0));
        }

        [Test]
        public void DoubleFlushIsNoOp()
        {
            // Arrange
            var service = new ChatReactionRecentsService(FIXED_DEFAULTS, MAX_RECENT);
            service.RecordUsage(10);

            // Act
            service.FlushIfDirty();
            service.FlushIfDirty(); // second flush should be a no-op

            // Assert
            var service2 = new ChatReactionRecentsService(FIXED_DEFAULTS, MAX_RECENT);
            Assert.That(service2.Recents.Count, Is.EqualTo(1));
        }

        // The old prefs format stored "index:count" pairs; the service should extract indices only.
        [Test]
        public void ParseLegacyFormatWithCounts()
        {
            // Arrange
            DCLPlayerPrefs.SetString(DCLPrefKeys.CHAT_REACTION_FAVORITES, "10:5;20:3");

            // Act
            var service = new ChatReactionRecentsService(FIXED_DEFAULTS, MAX_RECENT);

            // Assert
            Assert.That(service.Recents.Count, Is.EqualTo(2));
            Assert.That(service.Recents[0], Is.EqualTo(10));
            Assert.That(service.Recents[1], Is.EqualTo(20));
        }
    }
}
