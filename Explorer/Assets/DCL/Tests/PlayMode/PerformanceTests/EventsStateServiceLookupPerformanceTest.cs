using DCL.Communities.CommunitiesDataProvider.DTOs;
using ECS.TestSuite;
using DCL.Events;
using DCL.EventsApi;
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
    ///     Verifies the friend/community lookups in EventsStateService are O(1) dictionary hits (constant across
    ///     list size), match case-insensitively, resolve duplicate ids to the first inserted entry, and that a
    ///     null/empty community_id resolves to no match rather than throwing.
    /// </summary>
    public class EventsStateServiceLookupPerformanceTest
    {
        [OneTimeSetUp]
        public void OneTimeSetUp() => EcsTestsUtils.SetUpFeaturesRegistry();

        [OneTimeTearDown]
        public void OneTimeTearDown() => EcsTestsUtils.TearDownFeaturesRegistry();

        private const int EVENT_COUNT = 200;

        private static List<Profile.CompactInfo> BuildFriends(int count)
        {
            var friends = new List<Profile.CompactInfo>(count);
            for (var i = 0; i < count; i++)
                friends.Add(new Profile.CompactInfo(UserId.New($"0xFriend{i}").Unwrap(), $"Friend{i}"));
            return friends;
        }

        private static List<GetUserCommunitiesData.CommunityData> BuildCommunities(int count)
        {
            var communities = new List<GetUserCommunitiesData.CommunityData>(count);
            for (var i = 0; i < count; i++)
                communities.Add(new GetUserCommunitiesData.CommunityData { id = $"comm{i}", name = $"Community{i}" });
            return communities;
        }

        private static List<EventDTO> BuildEvents(int friendPoolSize)
        {
            var events = new List<EventDTO>(EVENT_COUNT);
            for (var i = 0; i < EVENT_COUNT; i++)
            {
                var addresses = new string[5];
                for (var a = 0; a < 5; a++)
                {
                    int idx = (i * 5 + a) % friendPoolSize;
                    string addr = $"0xFriend{idx}";
                    addresses[a] = a % 2 == 0 ? addr.ToUpperInvariant() : addr.ToLowerInvariant();
                }

                events.Add(new EventDTO
                {
                    id = $"ev{i}",
                    connected_addresses = addresses,
                    community_id = i % 2 == 0 ? $"comm{i % 50}" : null,
                });
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

        private static string? NaiveCommunity(List<GetUserCommunitiesData.CommunityData> communities, string communityId)
        {
            if (string.IsNullOrEmpty(communityId))
                return null;

            foreach (var c in communities)
                if (!string.IsNullOrEmpty(c.id) && c.id.Equals(communityId, StringComparison.OrdinalIgnoreCase))
                    return c.id;

            return null;
        }

        [Test]
        [Performance]
        public void GetEventDataById_1000Friends_PopulatePassIsConstantTime_AndOutputUnchanged()
        {
            List<EventDTO> events = BuildEvents(1000);
            List<Profile.CompactInfo> friends = BuildFriends(1000);
            List<GetUserCommunitiesData.CommunityData> communities = BuildCommunities(1000);

            using var service = new EventsStateService();
            service.AddEvents(events);
            service.SetAllFriends(friends);
            service.SetMyCommunities(communities);

            foreach (EventDTO e in events)
            {
                EventsStateService.EventWithPlaceAndFriendsData? data = service.GetEventDataById(EventId.New(e.id).Unwrap());
                Assert.IsNotNull(data);

                var expectedFriends = new List<string>();
                foreach (string addr in e.connected_addresses)
                    if (NaiveFriend(friends, addr, out Profile.CompactInfo m))
                        expectedFriends.Add(m.UserId);

                var actualFriends = new List<string>();
                foreach (Profile.CompactInfo f in data!.FriendsConnectedToPlace)
                    actualFriends.Add(f.UserId);

                CollectionAssert.AreEqual(expectedFriends, actualFriends, $"friend set mismatch for {e.id}");
                Assert.AreEqual(NaiveCommunity(communities, e.community_id), data.CommunityInfo?.id, $"community mismatch for {e.id}");
            }

            var dupFriends = new List<Profile.CompactInfo>
            {
                new (UserId.New("0xDUP").Unwrap(), "First"),
                new (UserId.New("0xdup").Unwrap(), "Second"),
            };
            using var dupService = new EventsStateService();
            dupService.SetAllFriends(dupFriends);
            dupService.AddEvents(new List<EventDTO> { new () { id = "evDup", connected_addresses = new[] { "0xDup" } } });
            EventsStateService.EventWithPlaceAndFriendsData? dupData = dupService.GetEventDataById(EventId.New("evDup").Unwrap());
            Assert.AreEqual(1, dupData!.FriendsConnectedToPlace.Count);
            Assert.AreEqual("First", dupData.FriendsConnectedToPlace[0].Name, "duplicate ids must resolve to the first inserted");

            double small = PopulatePassMs(10);
            double large = PopulatePassMs(1000);
            Measure.Custom(new SampleGroup("populate-friends-10", SampleUnit.Millisecond), small);
            Measure.Custom(new SampleGroup("populate-friends-1000", SampleUnit.Millisecond), large);
            Debug.Log($"[EventsStateService] populate pass: friends=10 -> {small:F3}ms, friends=1000 -> {large:F3}ms");

            Assert.LessOrEqual(large, 3 * small + 5, "dictionary lookups must not scale with friend-list size");
        }

        private static double PopulatePassMs(int friendPoolSize)
        {
            List<EventDTO> events = BuildEvents(friendPoolSize);
            List<Profile.CompactInfo> friends = BuildFriends(friendPoolSize);
            List<GetUserCommunitiesData.CommunityData> communities = BuildCommunities(friendPoolSize);

            using var service = new EventsStateService();
            service.AddEvents(events);
            service.SetAllFriends(friends);
            service.SetMyCommunities(communities);

            var sw = Stopwatch.StartNew();
            for (var rep = 0; rep < 50; rep++)
                foreach (EventDTO e in events)
                    service.GetEventDataById(EventId.New(e.id).Unwrap());
            sw.Stop();

            return sw.Elapsed.TotalMilliseconds;
        }
    }
}
