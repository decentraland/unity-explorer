using DCL.EventsApi;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace DCL.Tests.Editor
{
    // Order contract behind the Events-tab fix for
    // https://github.com/decentraland/unity-explorer/issues/9529: the comparer must define a
    // total order (id breaks every remaining tie) so that Array.Sort - which is unstable -
    // still produces the same sequence for the same content, no matter how the backend
    // happened to permute the response.
    [TestFixture]
    public class EventDisplayOrderComparerShould
    {
        private static readonly string[] TIMES =
        {
            "2026-08-10T10:00:00Z",
            "2026-08-11T09:30:00Z",
            "2026-08-12T18:00:00Z",
        };

        private static EventDTO Event(string id, bool live, string nextStartAt) =>
            new ()
            {
                id = id,
                live = live,
                NextStartAtProcessed = DateTime.Parse(nextStartAt, null, DateTimeStyles.RoundtripKind),
            };

        [Test]
        public void PutLiveEventsBeforeUpcomingOnes()
        {
            EventDTO[] events =
            {
                Event("upcoming-soon", live: false, TIMES[0]),
                Event("live-later", live: true, TIMES[2]),
            };

            Array.Sort(events, EventDisplayOrderComparer.INSTANCE);

            Assert.AreEqual("live-later", events[0].id,
                "A live event must come first even when its next occurrence is later than an upcoming event's.");
        }

        [Test]
        public void OrderBySoonestNextOccurrenceWithinTheSameLiveState()
        {
            EventDTO[] events =
            {
                Event("c", live: false, TIMES[2]),
                Event("a", live: false, TIMES[0]),
                Event("b", live: false, TIMES[1]),
            };

            Array.Sort(events, EventDisplayOrderComparer.INSTANCE);

            Assert.AreEqual(new[] { "a", "b", "c" }, Ids(events));
        }

        [Test]
        public void BreakFullKeyTiesByIdSoTheOrderIsTotal()
        {
            EventDTO[] events =
            {
                Event("zeta", live: false, TIMES[1]),
                Event("alpha", live: false, TIMES[1]),
                Event("mid", live: false, TIMES[1]),
            };

            Array.Sort(events, EventDisplayOrderComparer.INSTANCE);

            Assert.AreEqual(new[] { "alpha", "mid", "zeta" }, Ids(events));
        }

        [Test]
        public void ProduceTheSameSequenceForEveryInputPermutation()
        {
            // Includes same-timestamp pairs in both live states: exactly the inputs where an
            // unstable sort without a tiebreaker reshuffles between reopens.
            EventDTO[] content =
            {
                Event("live-tie-1", live: true, TIMES[0]),
                Event("live-tie-2", live: true, TIMES[0]),
                Event("upcoming-tie-1", live: false, TIMES[1]),
                Event("upcoming-tie-2", live: false, TIMES[1]),
                Event("upcoming-late", live: false, TIMES[2]),
                Event("live-late", live: true, TIMES[2]),
            };

            string[] expected = { "live-tie-1", "live-tie-2", "live-late", "upcoming-tie-1", "upcoming-tie-2", "upcoming-late" };

            // Rotations plus a reversed copy stand in for "whatever order the backend returned this time".
            for (var shift = 0; shift < content.Length; shift++)
            {
                EventDTO[] permutation = Rotate(content, shift);
                Array.Sort(permutation, EventDisplayOrderComparer.INSTANCE);

                Assert.AreEqual(expected, Ids(permutation), $"Rotation by {shift} sorted to a different sequence.");
            }

            EventDTO[] reversed = Rotate(content, 0);
            Array.Reverse(reversed);
            Array.Sort(reversed, EventDisplayOrderComparer.INSTANCE);
            Assert.AreEqual(expected, Ids(reversed), "The reversed input sorted to a different sequence.");
        }

        private static string[] Ids(IReadOnlyList<EventDTO> events)
        {
            var ids = new string[events.Count];

            for (var i = 0; i < events.Count; i++)
                ids[i] = events[i].id;

            return ids;
        }

        private static EventDTO[] Rotate(EventDTO[] source, int shift)
        {
            var rotated = new EventDTO[source.Length];

            for (var i = 0; i < source.Length; i++)
                rotated[i] = source[(i + shift) % source.Length];

            return rotated;
        }
    }
}
