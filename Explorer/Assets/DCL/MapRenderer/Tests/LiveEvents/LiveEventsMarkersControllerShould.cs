using Cysharp.Threading.Tasks;
using DCL.EventsApi;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using DCL.MapRenderer.MapLayers;
using DCL.MapRenderer.MapLayers.Categories;
using DCL.MapRenderer.MapLayers.Cluster;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Navmap;
using DCL.PlacesAPIService;
using DCL.WebRequests;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using Utility;

namespace DCL.MapRenderer.Tests.LiveEvents
{
    /// <summary>
    ///     Regression coverage for decentraland/unity-explorer#9538 ("Live event markers missing on the map when
    ///     many events are live"). <see cref="LiveEventsMarkersController" />'s poll method fetches all live
    ///     events (Land + WORLD, interleaved and ordered by start time) and must build a marker for every LAND
    ///     event regardless of where a WORLD event sorts in that list.
    ///     <br />
    ///     Pre-patch, the loop does `if (eventDto.world) return;` instead of `continue`, which exits the whole
    ///     polling method (not just the loop) the first time a WORLD event is seen - any LAND event ordered
    ///     after it in the response never gets a marker. This test feeds [world-event, land-event, land-event]
    ///     and expects both land markers to be created. It is expected to FAIL against the unpatched code (zero
    ///     markers created for either land event) and PASS once `return` is changed to `continue`
    ///     (potential-fix.patch).
    /// </summary>
    [TestFixture]
    public class LiveEventsMarkersControllerShould
    {
        private LiveEventsMarkersController controller;
        private IWebRequestController webRequestController;
        private CategoryIconMappingsSO categoryIconMappings;
        private CancellationTokenSource pollCts;
        private List<string> createdMarkerEventNames;

        [SetUp]
        public void SetUp()
        {
            createdMarkerEventNames = new List<string>();

            ICoordsUtils coordsUtils = Substitute.For<ICoordsUtils>();
            IMapCullingController cullingController = Substitute.For<IMapCullingController>();
            INavmapBus navmapBus = Substitute.For<INavmapBus>();

            LiveEventsMarkersController.CategoryMarkerBuilder builder = Substitute.For<LiveEventsMarkersController.CategoryMarkerBuilder>();

            builder.Invoke(Arg.Any<IObjectPool<CategoryMarkerObject>>(), Arg.Any<IMapCullingController>(), Arg.Any<ICoordsUtils>())
                   .Returns(_ =>
                    {
                        ICategoryMarker marker = Substitute.For<ICategoryMarker>();

                        // Record which event a marker was actually populated with, so the assertion doesn't
                        // depend on reflecting into the controller's private `markers` dictionary.
                        marker.When(m => m.SetData(Arg.Any<string>(), Arg.Any<Vector3>(), Arg.Any<PlacesData.PlaceInfo?>(), Arg.Any<EventDTO>()))
                              .Do(callInfo => createdMarkerEventNames.Add(callInfo.Arg<string>()));

                        return marker;
                    });

            categoryIconMappings = ScriptableObject.CreateInstance<CategoryIconMappingsSO>();
            categoryIconMappings.nftIcons = Array.Empty<SerializableKeyValuePair<MapLayer, Sprite>>();

            var clusterController = new ClusterController(
                cullingController,
                Substitute.For<IObjectPool<ClusterMarkerObject>>(),
                Substitute.For<CategoryMarkersController.ClusterMarkerBuilder>(),
                coordsUtils,
                navmapBus);

            webRequestController = Substitute.For<IWebRequestController>();

            IDecentralandUrlsSource urlsSource = Substitute.For<IDecentralandUrlsSource>();
            urlsSource.Url(Arg.Any<DecentralandUrl>()).Returns("https://events.decentraland.org/api/events");
            urlsSource.GetOriginalUrl(Arg.Any<string>()).Returns(callInfo => callInfo.Arg<string>());

            var eventsApiService = new HttpEventsApiService(webRequestController, urlsSource);

            controller = new LiveEventsMarkersController(
                eventsApiService,
                Substitute.For<IObjectPool<CategoryMarkerObject>>(),
                builder,
                null,
                coordsUtils,
                cullingController,
                categoryIconMappings,
                MapLayer.LiveEvents,
                clusterController,
                navmapBus);

            pollCts = new CancellationTokenSource();
        }

        [TearDown]
        public void TearDown()
        {
            pollCts.Cancel();
            pollCts.Dispose();
            controller?.Dispose();

            if (categoryIconMappings != null)
                UnityEngine.Object.DestroyImmediate(categoryIconMappings);
        }

        [Test]
        public void CreateMarkersForLandEventsAfterALiveWorldEvent()
        {
            var worldEvent = new EventDTO { name = "Live World Stage", world = true, x = 0, y = 0 };
            var landEventA = new EventDTO { name = "Land Event A", world = false, x = 10, y = 20 };
            var landEventB = new EventDTO { name = "Land Event B", world = false, x = 30, y = 40 };

            // Server order is next_start_at ASC and interleaves world/land events (see report.md) - the world
            // event sorting before the land events is exactly the "many live events" failure mode.
            ConfigureEventsResponse(worldEvent, landEventA, landEventB);

            InvokePoll();

            Assert.That(
                createdMarkerEventNames,
                Is.EqualTo(new[] { "Land Event A", "Land Event B" }),
                "A live world event ordered before land events must not abort marker creation for the land " +
                "events (#9538). Pre-patch this fails with an empty list: 'if (eventDto.world) return;' aborts " +
                "the whole poll method on the first world event instead of skipping just that one entry.");
        }

        private void ConfigureEventsResponse(params EventDTO[] events)
        {
            var response = new EventDTOListResponse { ok = true, data = events };

            webRequestController
               .SendAsync<GenericGetRequest, GenericGetArguments, GenericDownloadHandlerUtils.CreateFromJsonOp<EventDTOListResponse, GenericGetRequest>, EventDTOListResponse>(
                    Arg.Any<RequestEnvelope<GenericGetRequest, GenericGetArguments>>(),
                    Arg.Any<GenericDownloadHandlerUtils.CreateFromJsonOp<EventDTOListResponse, GenericGetRequest>>())
               .Returns(UniTask.FromResult(response));
        }

        private void InvokePoll()
        {
            MethodInfo pollMethod = typeof(LiveEventsMarkersController).GetMethod(
                "PollEventsAndPlacesOverTimeAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;

            // The mocked GetEventsAsync resolves as an already-completed UniTask, so this runs synchronously
            // (same call stack) all the way up to the first genuine suspension point - `await
            // UniTask.Delay(LIVE_EVENTS_POLLING_TIME, ...)`. That means every marker for this poll cycle is
            // already created by the time Invoke returns, exactly mirroring the synchronous portion of the
            // production `PollEventsAndPlacesOverTimeAsync(...).Forget()` call in EnableAsync.
            pollMethod.Invoke(controller, new object[] { pollCts.Token });
        }
    }
}
