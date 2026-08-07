using DCL.Events;
using ECS.TestSuite;
using DCL.EventsApi;
using DCL.Profiles;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using Unity.PerformanceTesting;
using Unity.Profiling;
using UnityEngine;

namespace DCL.Tests.PlayMode.PerformanceTests
{
    /// <summary>
    ///     Verifies GetEventDataById does not allocate a per-call empty friends List when connected_addresses is
    ///     null/empty (or has no matching friends): all such events share the single static EMPTY_FRIENDS instance,
    ///     confirmed by reference identity. Events with real matches still get their own list.
    /// </summary>
    public class EventsStateServiceAllocationPerformanceTest
    {
        [OneTimeSetUp]
        public void OneTimeSetUp() => EcsTestsUtils.SetUpFeaturesRegistry();

        [OneTimeTearDown]
        public void OneTimeTearDown() => EcsTestsUtils.TearDownFeaturesRegistry();

        private static EventsStateService BuildService()
        {
            var service = new EventsStateService();

            var friends = new List<Profile.CompactInfo>
            {
                new ("0xMatchA", "MatchA"),
                new ("0xMatchB", "MatchB"),
            };
            service.SetAllFriends(friends);

            var events = new List<EventDTO>();

            for (var i = 0; i < 10; i++)
                events.Add(new EventDTO { id = $"evNull{i}", connected_addresses = null });

            for (var i = 0; i < 10; i++)
                events.Add(new EventDTO { id = $"evEmpty{i}", connected_addresses = Array.Empty<string>() });

            for (var i = 0; i < 5; i++)
                events.Add(new EventDTO { id = $"evNoFriend{i}", connected_addresses = new[] { $"0xStranger{i}" } });

            events.Add(new EventDTO { id = "evMatch0", connected_addresses = new[] { "0xMatchA" } });
            events.Add(new EventDTO { id = "evMatch1", connected_addresses = new[] { "0xMatchA", "0xMatchB" } });

            service.AddEvents(events);
            return service;
        }

        [Test]
        [Performance]
        public void GetEventDataById_EmptyConnectedAddresses_AllocatesNoFriendLists()
        {
            using var service = BuildService();

            EventsStateService.EventWithPlaceAndFriendsData r1 = service.GetEventDataById("evNull0")!;
            EventsStateService.EventWithPlaceAndFriendsData r2 = service.GetEventDataById("evNull1")!;
            EventsStateService.EventWithPlaceAndFriendsData rEmpty = service.GetEventDataById("evEmpty0")!;
            EventsStateService.EventWithPlaceAndFriendsData rNoFriend = service.GetEventDataById("evNoFriend0")!;

            Assert.NotNull(r1.FriendsConnectedToPlace);
            Assert.AreEqual(0, r1.FriendsConnectedToPlace.Count);
            Assert.AreSame(r1.FriendsConnectedToPlace, r2.FriendsConnectedToPlace, "null-address events must share EMPTY_FRIENDS");
            Assert.AreSame(r1.FriendsConnectedToPlace, rEmpty.FriendsConnectedToPlace, "empty-address events must share EMPTY_FRIENDS");
            Assert.AreSame(r1.FriendsConnectedToPlace, rNoFriend.FriendsConnectedToPlace, "addresses-but-no-friends must share EMPTY_FRIENDS");

            EventsStateService.EventWithPlaceAndFriendsData rMatch = service.GetEventDataById("evMatch1")!;
            Assert.AreNotSame(r1.FriendsConnectedToPlace, rMatch.FriendsConnectedToPlace, "matched events must not use the shared instance");
            Assert.AreEqual(2, rMatch.FriendsConnectedToPlace.Count);

            var emptyIds = new List<string>();
            for (var i = 0; i < 10; i++) emptyIds.Add($"evNull{i}");
            for (var i = 0; i < 10; i++) emptyIds.Add($"evEmpty{i}");

            ProfilerRecorder gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc");

            Measure.Method(() =>
                    {
                        foreach (string id in emptyIds)
                            service.GetEventDataById(id);
                    })
                   .WarmupCount(5)
                   .MeasurementCount(20)
                   .GC()
                   .Run();

            long batchAlloc = gcAlloc.LastValue;
            gcAlloc.Dispose();
            Debug.Log($"[EventsStateService] empty-address batch (20 events) GC.Alloc last: {batchAlloc} bytes (no per-event friend-list allocations)");
        }
    }
}
