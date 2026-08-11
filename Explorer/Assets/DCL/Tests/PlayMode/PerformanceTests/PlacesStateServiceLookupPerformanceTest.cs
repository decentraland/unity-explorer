using DCL.EventsApi;
using ECS.TestSuite;
using DCL.Places;
using DCL.PlacesAPIService;
using DCL.Profiles;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.PerformanceTesting;
using Debug = UnityEngine.Debug;

namespace DCL.Tests.PlayMode.PerformanceTests
{
    /// <summary>
    ///     Verifies PlacesStateService friend/live-event lookups are O(1) dictionary hits (constant across list
    ///     size), match case-insensitively across all three call paths (AddPlaces, RefreshFriendsData,
    ///     RefreshLiveEventsData), and that a null/empty place_id resolves to no match rather than throwing.
    /// </summary>
    public class PlacesStateServiceLookupPerformanceTest
    {
        [OneTimeSetUp]
        public void OneTimeSetUp() => EcsTestsUtils.SetUpFeaturesRegistry();

        [OneTimeTearDown]
        public void OneTimeTearDown() => EcsTestsUtils.TearDownFeaturesRegistry();

        private const int PLACE_COUNT = 200;

        private static List<Profile.CompactInfo> BuildFriends(int count)
        {
            var friends = new List<Profile.CompactInfo>(count);
            for (var i = 0; i < count; i++)
                friends.Add(new Profile.CompactInfo(UserId.New($"0xFriend{i}").Unwrap(), $"Friend{i}"));
            return friends;
        }

        private static List<PlacesData.PlaceInfo> BuildPlaces(int friendPoolSize)
        {
            var places = new List<PlacesData.PlaceInfo>(PLACE_COUNT);
            for (var i = 0; i < PLACE_COUNT; i++)
            {
                string[]? addresses;
                if (i % 20 == 0)
                    addresses = null;
                else
                {
                    addresses = new string[5];
                    for (var a = 0; a < 5; a++)
                    {
                        int idx = (i * 5 + a) % friendPoolSize;
                        string addr = $"0xFriend{idx}";
                        addresses[a] = a % 2 == 0 ? addr.ToUpperInvariant() : addr.ToLowerInvariant();
                    }
                }

                places.Add(new PlacesData.PlaceInfo(default) { id = $"p{i}", connected_addresses = addresses });
            }

            return places;
        }

        private static List<EventDTO> BuildLiveEvents(int count)
        {
            var events = new List<EventDTO>(count);
            for (var i = 0; i < count; i++)
            {
                string? placeId = i switch
                {
                    _ when i % 97 == 0 => null,
                    _ when i % 89 == 0 => string.Empty,
                    _ => (i % 2 == 0 ? $"P{i % 50}" : $"p{i % 50}"),
                };
                events.Add(new EventDTO { id = $"ev{i}", place_id = placeId });
            }

            return events;
        }

        private static bool NaiveFriend(List<Profile.CompactInfo> friends, string userId, out Profile.CompactInfo match)
        {
            foreach (var f in friends)
                if (!string.IsNullOrEmpty(f.UserId) && string.Equals(f.UserId, userId, StringComparison.OrdinalIgnoreCase))
                {
                    match = f;
                    return true;
                }

            match = default;
            return false;
        }

        private static string? NaiveLiveEventId(List<EventDTO> events, string placeId)
        {
            if (string.IsNullOrEmpty(placeId))
                return null;

            foreach (var e in events)
                if (!string.IsNullOrEmpty(e.place_id) && e.place_id.Equals(placeId, StringComparison.OrdinalIgnoreCase))
                    return e.id;

            return null;
        }

        private static List<string> ExpectedFriends(PlacesData.PlaceInfo place, List<Profile.CompactInfo> friends)
        {
            var result = new List<string>();
            if (place.connected_addresses != null)
                foreach (string addr in place.connected_addresses)
                    if (NaiveFriend(friends, addr, out Profile.CompactInfo m))
                        result.Add(m.UserId);
            return result;
        }

        [Test]
        [Performance]
        public void AddPlaces_200Places_1000FriendsAndLiveEvents_IsConstantTime_AndOutputUnchanged()
        {
            List<PlacesData.PlaceInfo> places = BuildPlaces(1000);
            List<Profile.CompactInfo> friends = BuildFriends(1000);
            List<EventDTO> liveEvents = BuildLiveEvents(1000);

            using var service = new PlacesStateService();
            service.SetAllFriends(friends);
            service.SetLiveEvents(liveEvents);
            service.AddPlaces(places);

            foreach (PlacesData.PlaceInfo place in places)
            {
                PlacesStateService.PlaceInfoWithConnectedFriends entry = service.CurrentPlaces[PlaceId.New(place.id).Unwrap()];

                var expectedFriends = ExpectedFriends(place, friends);
                var actualFriends = new List<string>();
                foreach (Profile.CompactInfo f in entry.ConnectedFriends)
                    actualFriends.Add(f.UserId);

                CollectionAssert.AreEqual(expectedFriends, actualFriends, $"friend set mismatch for {place.id}");
                Assert.AreEqual(NaiveLiveEventId(liveEvents, place.id), entry.LiveEvent?.id, $"live event mismatch for {place.id}");
            }

            List<Profile.CompactInfo> otherFriends = BuildFriends(500);
            service.SetAllFriends(otherFriends);
            service.RefreshFriendsData();
            service.RefreshLiveEventsData();

            foreach (PlacesData.PlaceInfo place in places)
            {
                PlacesStateService.PlaceInfoWithConnectedFriends entry = service.CurrentPlaces[PlaceId.New(place.id).Unwrap()];

                var expectedFriends = ExpectedFriends(place, otherFriends);
                var actualFriends = new List<string>();
                foreach (Profile.CompactInfo f in entry.ConnectedFriends)
                    actualFriends.Add(f.UserId);

                CollectionAssert.AreEqual(expectedFriends, actualFriends, $"refreshed friend set mismatch for {place.id}");
                Assert.AreEqual(NaiveLiveEventId(liveEvents, place.id), entry.LiveEvent?.id, $"refreshed live event mismatch for {place.id}");
            }

            double small = AddPlacesPassMs(10);
            double large = AddPlacesPassMs(1000);
            Measure.Custom(new SampleGroup("addplaces-friends-10", SampleUnit.Millisecond), small);
            Measure.Custom(new SampleGroup("addplaces-friends-1000", SampleUnit.Millisecond), large);
            Debug.Log($"[PlacesStateService] AddPlaces pass: friends=10 -> {small:F3}ms, friends=1000 -> {large:F3}ms");

            Assert.LessOrEqual(large, 3 * small + 5, "dictionary lookups must not scale with friend/live-event list size");
        }

        private static double AddPlacesPassMs(int poolSize)
        {
            List<PlacesData.PlaceInfo> places = BuildPlaces(poolSize);
            List<Profile.CompactInfo> friends = BuildFriends(poolSize);
            List<EventDTO> liveEvents = BuildLiveEvents(poolSize);

            using var service = new PlacesStateService();
            service.SetAllFriends(friends);
            service.SetLiveEvents(liveEvents);

            var sw = Stopwatch.StartNew();
            for (var rep = 0; rep < 50; rep++)
            {
                service.ClearPlaces();
                service.AddPlaces(places);
            }
            sw.Stop();

            return sw.Elapsed.TotalMilliseconds;
        }
    }
}
