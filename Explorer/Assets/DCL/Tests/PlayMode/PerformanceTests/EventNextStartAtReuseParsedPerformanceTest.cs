using DCL.Events;
using DCL.EventsApi;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Unity.PerformanceTesting;
using Debug = UnityEngine.Debug;

namespace DCL.Tests.PlayMode.PerformanceTests
{
    /// <summary>
    ///     Verifies EventsCalendarController.GetEventLocalDate (reusing the already-parsed NextStartAtProcessed)
    ///     yields the same wall-clock/day bucket as DateTimeOffset.Parse(next_start_at).ToLocalTime() for both
    ///     'Z' (Utc-kind) and offset (Local-kind) forms, and is cheaper than parsing on every call site.
    /// </summary>
    public class EventNextStartAtReuseParsedPerformanceTest
    {
        private static readonly string[] SAMPLES =
        {
            "2026-08-07T18:00:00Z",
            "2026-08-07T18:00:00.123Z",
            "2026-08-07T18:00:00+02:00",
            "2026-08-07T18:00:00-05:00",
            "2026-12-31T23:59:59Z",
        };

        private static EventDTO MakeDto(string nextStartAt)
        {
            var dto = new EventDTO { next_start_at = nextStartAt };
            DateTime.TryParse(nextStartAt, null, DateTimeStyles.RoundtripKind, out DateTime parsed);
            dto.NextStartAtProcessed = parsed;
            return dto;
        }

        [Test]
        [Performance]
        public void GetEventLocalDate_MatchesDateTimeOffsetParse_AndIsCheaperThanReparsing()
        {
            foreach (string s in SAMPLES)
            {
                EventDTO dto = MakeDto(s);
                DateTime expected = DateTimeOffset.Parse(s).ToLocalTime().DateTime;
                DateTime actual = EventsCalendarController.GetEventLocalDate(in dto);

                Assert.AreEqual(expected, actual, $"wall-clock mismatch for '{s}'");
                Assert.AreEqual(expected.Date, actual.Date, $"day bucket mismatch for '{s}'");
            }

            var events = new List<EventDTO>(100);
            var baseDate = new DateTime(2026, 8, 3, 12, 0, 0, DateTimeKind.Utc);
            for (var i = 0; i < 100; i++)
            {
                DateTime d = baseDate.AddDays(i % 5).AddMinutes(i);
                string s = i % 2 == 0
                    ? d.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
                    : new DateTimeOffset(d).ToOffset(TimeSpan.FromHours(2)).ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);
                events.Add(MakeDto(s));
            }

            foreach (EventDTO dto in events)
            {
                DateTime legacyBucket = DateTimeOffset.Parse(dto.next_start_at).ToLocalTime().DateTime.Date;
                DateTime processedBucket = EventsCalendarController.GetEventLocalDate(in dto).Date;
                Assert.AreEqual(legacyBucket, processedBucket, $"day-bucket assignment diverged for '{dto.next_start_at}'");
            }

            const int REPS = 20;
            var swProcessed = new Stopwatch();
            var swLegacy = new Stopwatch();

            foreach (EventDTO dto in events) { _ = EventsCalendarController.GetEventLocalDate(in dto); _ = DateTimeOffset.Parse(dto.next_start_at).ToLocalTime().DateTime; }

            swProcessed.Start();
            for (var r = 0; r < REPS; r++)
                foreach (EventDTO dto in events)
                    for (var site = 0; site < 3; site++)
                        _ = EventsCalendarController.GetEventLocalDate(in dto);
            swProcessed.Stop();

            swLegacy.Start();
            for (var r = 0; r < REPS; r++)
                foreach (EventDTO dto in events)
                    for (var site = 0; site < 3; site++)
                        _ = DateTimeOffset.Parse(dto.next_start_at).ToLocalTime().DateTime;
            swLegacy.Stop();

            double processedMs = swProcessed.Elapsed.TotalMilliseconds;
            double legacyMs = swLegacy.Elapsed.TotalMilliseconds;
            Measure.Custom(new SampleGroup("processed-reuse", SampleUnit.Millisecond), processedMs);
            Measure.Custom(new SampleGroup("legacy-reparse", SampleUnit.Millisecond), legacyMs);
            Debug.Log($"[EventsCalendarController] processed-reuse={processedMs:F3}ms, legacy-reparse={legacyMs:F3}ms");

            Assert.Less(processedMs, legacyMs, "reusing the parsed DateTime must be cheaper than DateTimeOffset.Parse");
        }
    }
}
