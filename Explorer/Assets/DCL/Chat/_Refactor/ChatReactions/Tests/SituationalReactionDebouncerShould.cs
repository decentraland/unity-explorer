using System.Collections.Generic;
using DCL.Chat.ChatReactions.Core;
using NUnit.Framework;

namespace DCL.Chat.ChatReactions.Tests
{
    [TestFixture]
    public class SituationalReactionDebouncerShould
    {
        private Dictionary<int, int>? lastFlushed;
        private int flushCount;
        private float debounceSeconds;

        [SetUp]
        public void SetUp()
        {
            lastFlushed = null;
            flushCount = 0;
            debounceSeconds = 0.5f;
        }

        private SituationalReactionDebouncer CreateDebouncer() =>
            new (CaptureFlush, () => debounceSeconds);

        private void CaptureFlush(Dictionary<int, int> emojis)
        {
            lastFlushed = new Dictionary<int, int>(emojis);
            flushCount++;
        }

        [Test]
        public void FlushImmediatelyWhenDebounceIsZero()
        {
            debounceSeconds = 0f;
            var debouncer = CreateDebouncer();

            debouncer.Add(3);

            Assert.That(flushCount, Is.EqualTo(1));
            Assert.That(lastFlushed![3], Is.EqualTo(1));
        }

        [Test]
        public void AccumulateSameEmojiCount()
        {
            debounceSeconds = 1f;
            var debouncer = CreateDebouncer();

            debouncer.Add(5);
            debouncer.Add(5);
            debouncer.Add(5);
            debouncer.Tick(1.1f);

            Assert.That(flushCount, Is.EqualTo(1));
            Assert.That(lastFlushed![5], Is.EqualTo(3));
        }

        [Test]
        public void TrackMultipleDistinctEmojis()
        {
            debounceSeconds = 1f;
            var debouncer = CreateDebouncer();

            debouncer.Add(1);
            debouncer.Add(2);
            debouncer.Add(3);
            debouncer.Tick(1.1f);

            Assert.That(flushCount, Is.EqualTo(1));
            Assert.That(lastFlushed!.Count, Is.EqualTo(3));
            Assert.That(lastFlushed[1], Is.EqualTo(1));
            Assert.That(lastFlushed[2], Is.EqualTo(1));
            Assert.That(lastFlushed[3], Is.EqualTo(1));
        }

        [Test]
        public void FlushAfterTimerExpires()
        {
            debounceSeconds = 0.5f;
            var debouncer = CreateDebouncer();

            debouncer.Add(7);
            debouncer.Tick(0.6f);

            Assert.That(flushCount, Is.EqualTo(1));
            Assert.That(lastFlushed![7], Is.EqualTo(1));
        }

        [Test]
        public void NotFlushBeforeTimerExpires()
        {
            debounceSeconds = 0.5f;
            var debouncer = CreateDebouncer();

            debouncer.Add(7);
            debouncer.Tick(0.3f);

            Assert.That(flushCount, Is.EqualTo(0));
            Assert.That(lastFlushed, Is.Null);
        }

        [Test]
        public void ResetBufferAfterFlush()
        {
            debounceSeconds = 0.5f;
            var debouncer = CreateDebouncer();

            debouncer.Add(1);
            debouncer.Tick(0.6f);

            Assert.That(flushCount, Is.EqualTo(1));

            // Tick again — should NOT flush again
            debouncer.Tick(0.6f);

            Assert.That(flushCount, Is.EqualTo(1));
        }

        [Test]
        public void StartNewBufferAfterFlush()
        {
            debounceSeconds = 0.5f;
            var debouncer = CreateDebouncer();

            debouncer.Add(1);
            debouncer.Tick(0.6f);
            Assert.That(flushCount, Is.EqualTo(1));
            Assert.That(lastFlushed![1], Is.EqualTo(1));

            // New buffer cycle
            debouncer.Add(2);
            debouncer.Tick(0.6f);
            Assert.That(flushCount, Is.EqualTo(2));
            Assert.That(lastFlushed![2], Is.EqualTo(1));
            Assert.That(lastFlushed.ContainsKey(1), Is.False, "Previous emoji should not carry over");
        }

        [Test]
        public void ClearBufferWithoutFlushingOnDispose()
        {
            debounceSeconds = 10f;
            var debouncer = CreateDebouncer();

            debouncer.Add(4);
            debouncer.Add(4);
            debouncer.Dispose();

            Assert.That(flushCount, Is.EqualTo(0));
            Assert.That(lastFlushed, Is.Null);
        }

        [Test]
        public void NotFlushOnDisposeWhenEmpty()
        {
            var debouncer = CreateDebouncer();

            debouncer.Dispose();

            Assert.That(flushCount, Is.EqualTo(0));
            Assert.That(lastFlushed, Is.Null);
        }

        [Test]
        public void ResetTimerOnNewAdd()
        {
            debounceSeconds = 0.5f;
            var debouncer = CreateDebouncer();

            debouncer.Add(1);
            debouncer.Tick(0.4f); // timer: 0.5 - 0.4 = 0.1 remaining

            Assert.That(flushCount, Is.EqualTo(0));

            debouncer.Add(2); // resets timer to 0.5

            debouncer.Tick(0.4f); // timer: 0.5 - 0.4 = 0.1 remaining — should NOT flush yet

            Assert.That(flushCount, Is.EqualTo(0));

            debouncer.Tick(0.2f); // timer: 0.1 - 0.2 = -0.1 — NOW it flushes

            Assert.That(flushCount, Is.EqualTo(1));
            Assert.That(lastFlushed!.Count, Is.EqualTo(2));
            Assert.That(lastFlushed[1], Is.EqualTo(1));
            Assert.That(lastFlushed[2], Is.EqualTo(1));
        }
    }
}
