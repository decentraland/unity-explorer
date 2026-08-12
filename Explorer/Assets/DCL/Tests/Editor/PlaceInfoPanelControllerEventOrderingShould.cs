using Cysharp.Threading.Tasks;
using DCL.EventsApi;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Navmap;
using DCL.PlacesAPIService;
using DCL.WebRequests;
using DCL.WebRequests.RequestsHub;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.Tests.Editor
{
    // Regression coverage for https://github.com/decentraland/unity-explorer/issues/9529:
    // "Map -> Genesis Plaza -> Events tab: events out of order, order changes on every reopen".
    //
    // PlaceInfoPanelController's Events-tab loading skeleton (SetAsLoadingState(), a local
    // function inside the private FetchAndShowEventsOfThePlace()) pulls rows from a
    // stack-based UnityEngine.Pool.ObjectPool<EventElementView> without ever repositioning
    // them under their parent, so a reused row keeps whatever transform-sibling slot it last
    // occupied. Because the pool is LIFO, releasing N rows in list order and then
    // re-acquiring N rows hands them back in the exact reverse order - so after any "reopen"
    // the row the controller *thinks* is first (eventElements[0]) is not the GameObject that
    // is actually first on screen (sibling index 0). The fix adds
    // `element.transform.SetAsLastSibling()` right after every `eventElementPool.Get()` (both
    // in this skeleton loop and in the main fetch-result loop) so on-screen order always
    // tracks population order.
    //
    // This test drives the real, unmodified SetAsLoadingState()/FetchAndShowEventsOfThePlace()
    // through reflection: FetchAndShowEventsOfThePlace is private but its name and signature
    // are untouched by the patch (only its body changes), so reflecting into it is stable
    // across both the unpatched and patched source - unlike its compiler-generated local
    // functions, whose generated names could shift with the patch. The heavy constructor
    // (~15 unrelated view/service dependencies) is bypassed via FormatterServices
    // .GetUninitializedObject - already precedented in this repo, see
    // NearbyAudioPerformanceManualTest.cs - and only the handful of private fields this one
    // code path actually touches are injected.
    //
    // Scope: this covers the loading-skeleton half of the patch (SetAsLastSibling in
    // SetAsLoadingState). The sort's order contract is covered separately by
    // EventDisplayOrderComparerShould; driving the main fetch-result loop with real event
    // data would additionally require a fully wired EventElementView hierarchy.
    [TestFixture]
    public class PlaceInfoPanelControllerEventOrderingShould
    {
        private const int SKELETON_ROW_COUNT = 8;

        private static readonly MethodInfo FetchAndShowEventsMethod =
            typeof(PlaceInfoPanelController).GetMethod("FetchAndShowEventsOfThePlace", BindingFlags.NonPublic | BindingFlags.Instance)!;

        private static readonly FieldInfo EventElementsField =
            typeof(PlaceInfoPanelController).GetField("eventElements", BindingFlags.NonPublic | BindingFlags.Instance)!;

        private GameObject poolParentGO;
        private Transform poolParent;
        private GameObject templateGO;
        private ObjectPool<EventElementView> pool;
        private PlaceInfoPanelController controller;

        [SetUp]
        public void SetUp()
        {
            poolParentGO = new GameObject("EventsContentContainer");
            poolParent = poolParentGO.transform;

            templateGO = new GameObject("EventElementViewTemplate");
            EventElementView template = templateGO.AddComponent<EventElementView>();
            templateGO.SetActive(false);

            pool = new ObjectPool<EventElementView>(
                () => Object.Instantiate(template, poolParent),
                actionOnGet: result => result.gameObject.SetActive(true),
                actionOnRelease: result => result.gameObject.SetActive(false),
                defaultCapacity: SKELETON_ROW_COUNT);

            // Hermetic double for the one HTTP fetch this code path performs, following the
            // same IWebRequestController.SendAsync<...> generic-method stubbing idiom already
            // used in DCL/Profiles/Tests/RealmProfileRepositoryShould.cs. The stubbed task is
            // deliberately left forever-pending: this test only needs the synchronous
            // pre-await portion of FetchAndShowEventsOfThePlace (the loading skeleton), so the
            // event payload never has to arrive.
            IWebRequestController webRequestController = Substitute.For<IWebRequestController>();
            webRequestController.RequestHub.Returns(Substitute.For<IRequestHub>());

            IDecentralandUrlsSource urlsSource = Substitute.For<IDecentralandUrlsSource>();
            urlsSource.Url(Arg.Any<DecentralandUrl>()).Returns("https://events.test.local/api/events");
            urlsSource.GetOriginalUrl(Arg.Any<string>()).Returns("https://events.test.local/api/events");

            webRequestController
               .SendAsync<GenericGetRequest, GenericGetArguments, GenericDownloadHandlerUtils.CreateFromJsonOp<EventDTOListResponse, GenericGetRequest>, EventDTOListResponse>(
                    Arg.Any<RequestEnvelope<GenericGetRequest, GenericGetArguments>>(),
                    Arg.Any<GenericDownloadHandlerUtils.CreateFromJsonOp<EventDTOListResponse, GenericGetRequest>>())!
               .Returns(new UniTaskCompletionSource<EventDTOListResponse>().Task);

            var eventsApiService = new HttpEventsApiService(webRequestController, urlsSource);

            PlaceInfoPanelView view = new GameObject("PlaceInfoPanelView").AddComponent<PlaceInfoPanelView>();
            SetPrivate(view, "EmptyEventsContainer", new GameObject("EmptyEventsContainer"));

            // Bypasses PlaceInfoPanelController's heavy constructor (it wires ~15 view
            // buttons/services unrelated to this code path) and instead injects only the
            // fields FetchAndShowEventsOfThePlace()/SetAsLoadingState() actually touch.
            controller = (PlaceInfoPanelController) FormatterServices.GetUninitializedObject(typeof(PlaceInfoPanelController));
            SetPrivate(controller, "view", view);
            SetPrivate(controller, "eventElementPool", pool);
            SetPrivate(controller, "eventElements", new List<EventElementView>());
            SetPrivate(controller, "place", new PlacesData.PlaceInfo(Vector2Int.zero));
            SetPrivate(controller, "eventsApiService", eventsApiService);
        }

        [TearDown]
        public void TearDown()
        {
            if (poolParentGO != null) Object.DestroyImmediate(poolParentGO);
            if (templateGO != null) Object.DestroyImmediate(templateGO);
        }

        [Test]
        public void KeepOnScreenSiblingOrderInSyncWithPopulationOrderAcrossReopens()
        {
            InvokeFetchAndShowEvents(); // first open: pool starts empty -> 8 fresh rows instantiated

            var round1 = new List<EventElementView>(CurrentEventElements());
            Assert.AreEqual(SKELETON_ROW_COUNT, round1.Count, "Precondition: the loading skeleton should populate 8 rows.");

            // Mirrors what ClearEventElements() does between one Events-tab open and the
            // next: release every row, in the order they were added.
            foreach (EventElementView element in round1)
                pool.Release(element);

            CurrentEventElements().Clear();

            InvokeFetchAndShowEvents(); // "reopen": the pool now hands back the same 8 rows, LIFO.

            var round2 = new List<EventElementView>(CurrentEventElements());
            Assert.AreEqual(SKELETON_ROW_COUNT, round2.Count);
            CollectionAssert.AreEquivalent(round1, round2, "Reopening should reuse the same pooled rows, not instantiate new ones.");

            Assert.AreEqual(SKELETON_ROW_COUNT, poolParent.childCount, "No extra rows should have been instantiated.");

            var onScreenOrder = new List<EventElementView>();
            for (var i = 0; i < poolParent.childCount; i++)
                onScreenOrder.Add(poolParent.GetChild(i).GetComponent<EventElementView>());

            CollectionAssert.AreEqual(round2, onScreenOrder,
                "On-screen (transform sibling) order must match the order the controller just populated " +
                "eventElements in. Unpatched code leaves reused pooled rows at their stale sibling slot " +
                "instead of calling SetAsLastSibling() after eventElementPool.Get(), so a reopen silently " +
                "permutes what's on screen relative to what the controller thinks it's showing.");
        }

        private void InvokeFetchAndShowEvents() =>
            FetchAndShowEventsMethod.Invoke(controller, null);

        private List<EventElementView> CurrentEventElements() =>
            (List<EventElementView>) EventElementsField.GetValue(controller);

        private static void SetPrivate(object target, string memberName, object value)
        {
            const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            PropertyInfo property = target.GetType().GetProperty(memberName, FLAGS);

            if (property != null)
            {
                property.GetSetMethod(true)!.Invoke(target, new[] { value });
                return;
            }

            FieldInfo field = target.GetType().GetField(memberName, FLAGS)!;
            field.SetValue(target, value);
        }
    }
}
