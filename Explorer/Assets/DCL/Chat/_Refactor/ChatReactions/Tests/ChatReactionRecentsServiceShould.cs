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

        [Test]
        public void IgnoreFixedDefaults()
        {
            var service = new ChatReactionRecentsService(FIXED_DEFAULTS, MAX_RECENT);
            service.RecordUsage(0); // fixed default
            service.RecordUsage(1); // fixed default
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
            var service = new ChatReactionRecentsService(FIXED_DEFAULTS, MAX_RECENT);

            service.RecordUsage(10);
            service.RecordUsage(20);
            service.RecordUsage(30);

            Assert.That(service.Recents[0], Is.EqualTo(30)); // most recent
            Assert.That(service.Recents[1], Is.EqualTo(20));
            Assert.That(service.Recents[2], Is.EqualTo(10)); // oldest
        }

        [Test]
        public void MoveToFrontOnReuse()
        {
            var service = new ChatReactionRecentsService(FIXED_DEFAULTS, MAX_RECENT);

            service.RecordUsage(10);
            service.RecordUsage(20);
            service.RecordUsage(30);

            // Re-use 10 — it should jump to front.
            service.RecordUsage(10);

            Assert.That(service.Recents[0], Is.EqualTo(10));
            Assert.That(service.Recents[1], Is.EqualTo(30));
            Assert.That(service.Recents[2], Is.EqualTo(20));
            Assert.That(service.Recents.Count, Is.EqualTo(MAX_RECENT));
        }

        [Test]
        public void DropOldestWhenCapacityExceeded()
        {
            var service = new ChatReactionRecentsService(FIXED_DEFAULTS, MAX_RECENT);

            service.RecordUsage(10);
            service.RecordUsage(20);
            service.RecordUsage(30);
            service.RecordUsage(40); // 10 should be evicted

            Assert.That(service.Recents.Count, Is.EqualTo(MAX_RECENT));
            Assert.That(service.Recents[0], Is.EqualTo(40));
            Assert.That(service.Recents[1], Is.EqualTo(30));
            Assert.That(service.Recents[2], Is.EqualTo(20));
        }

        [Test]
        public void NotPersistUntilFlushed()
        {
            var service = new ChatReactionRecentsService(FIXED_DEFAULTS, MAX_RECENT);
            service.RecordUsage(10);
            service.RecordUsage(20);

            // Create a second instance — reads from prefs.
            // If save was deferred, the second instance should NOT see the data.
            var service2 = new ChatReactionRecentsService(FIXED_DEFAULTS, MAX_RECENT);
            Assert.That(service2.Recents.Count, Is.EqualTo(0));
        }

        [Test]
        public void PersistAfterFlush()
        {
            var service = new ChatReactionRecentsService(FIXED_DEFAULTS, MAX_RECENT);
            service.RecordUsage(10);
            service.RecordUsage(20);
            service.FlushIfDirty();

            // New instance should see the persisted data in recency order.
            var service2 = new ChatReactionRecentsService(FIXED_DEFAULTS, MAX_RECENT);
            Assert.That(service2.Recents.Count, Is.EqualTo(2));
            Assert.That(service2.Recents[0], Is.EqualTo(20)); // most recent
            Assert.That(service2.Recents[1], Is.EqualTo(10));
        }

        [Test]
        public void FlushIsNoOpWhenClean()
        {
            var service = new ChatReactionRecentsService(FIXED_DEFAULTS, MAX_RECENT);
            // No RecordUsage calls — nothing dirty.
            service.FlushIfDirty(); // should not throw or write

            var service2 = new ChatReactionRecentsService(FIXED_DEFAULTS, MAX_RECENT);
            Assert.That(service2.Recents.Count, Is.EqualTo(0));
        }

        [Test]
        public void DoubleFlushIsNoOp()
        {
            var service = new ChatReactionRecentsService(FIXED_DEFAULTS, MAX_RECENT);
            service.RecordUsage(10);
            service.FlushIfDirty();
            service.FlushIfDirty(); // second flush — should be no-op, no error

            var service2 = new ChatReactionRecentsService(FIXED_DEFAULTS, MAX_RECENT);
            Assert.That(service2.Recents.Count, Is.EqualTo(1));
        }

        [Test]
        public void ParseLegacyFormatWithCounts()
        {
            // Simulate old format: "10:5;20:3" (index:count pairs)
            DCLPlayerPrefs.SetString(DCLPrefKeys.CHAT_REACTION_FAVORITES, "10:5;20:3");

            var service = new ChatReactionRecentsService(FIXED_DEFAULTS, MAX_RECENT);

            // Should pick up the indices (10, 20) and ignore the counts.
            Assert.That(service.Recents.Count, Is.EqualTo(2));
            Assert.That(service.Recents[0], Is.EqualTo(10));
            Assert.That(service.Recents[1], Is.EqualTo(20));
        }
    }
}
