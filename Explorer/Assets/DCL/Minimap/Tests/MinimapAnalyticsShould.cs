using Cysharp.Threading.Tasks;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.PerformanceAndDiagnostics.Analytics.EventBased;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NUnit.Framework;
using System.Threading.Tasks;
using Utility;

namespace DCL.Minimap.Tests
{
    [TestFixture]
    public class MinimapAnalyticsShould
    {
        private const int DEBOUNCE_MS = 50;
        private const int DEBOUNCE_WAIT_MS = 300;

        private IAnalyticsController analytics = null!;
        private IEventBus eventBus = null!;
        private MinimapAnalytics sut = null!;

        [SetUp]
        public void SetUp()
        {
            analytics = Substitute.For<IAnalyticsController>();
            eventBus = new EventBus(invokeSubscribersOnMainThread: false);
            sut = new MinimapAnalytics(analytics, eventBus, DEBOUNCE_MS);
        }

        [TearDown]
        public void TearDown() =>
            sut.Dispose();

        [Test]
        public void TrackActionsWithoutPayload()
        {
            eventBus.Publish(new MinimapEvents.Expanded());
            eventBus.Publish(new MinimapEvents.Collapsed());
            eventBus.Publish(new MinimapEvents.MapClicked());
            eventBus.Publish(new MinimapEvents.ContextMenuOpened());

            analytics.Received(1).Track(AnalyticsEvents.Minimap.EXPANDED);
            analytics.Received(1).Track(AnalyticsEvents.Minimap.COLLAPSED);
            analytics.Received(1).Track(AnalyticsEvents.Minimap.MAP_CLICKED);
            analytics.Received(1).Track(AnalyticsEvents.Minimap.CONTEXT_MENU_OPENED);
        }

        [Test]
        public void TrackFavoriteState()
        {
            eventBus.Publish(new MinimapEvents.FavoriteClicked(true));

            analytics.Received(1).Track(AnalyticsEvents.Minimap.FAVORITE_CLICKED, Arg.Is<JObject>(o => (bool)o["is_favorite"]!));
        }

        [Test]
        public void TrackSkyboxWidgetState()
        {
            eventBus.Publish(new MinimapEvents.SkyboxWidgetToggled(true));

            analytics.Received(1).Track(AnalyticsEvents.Minimap.SKYBOX_WIDGET_TOGGLED, Arg.Is<JObject>(o => (bool)o["is_open"]!));
        }

        [Test]
        public void TrackSkyboxDayCycleState()
        {
            eventBus.Publish(new MinimapEvents.SkyboxDayCycleToggled(true));

            analytics.Received(1).Track(AnalyticsEvents.Minimap.SKYBOX_DAY_CYCLE_TOGGLED, Arg.Is<JObject>(o => (bool)o["is_enabled"]!));
        }

        [Test]
        public async Task TrackOnlyTheSettledSkyboxTime()
        {
            eventBus.Publish(new MinimapEvents.SkyboxTimeChanged(0.2f));
            eventBus.Publish(new MinimapEvents.SkyboxTimeChanged(0.3f));
            eventBus.Publish(new MinimapEvents.SkyboxTimeChanged(0.5f));

            analytics.DidNotReceiveWithAnyArgs().Track(default!);

            await UniTask.Delay(DEBOUNCE_WAIT_MS);

            analytics.Received(1).Track(AnalyticsEvents.Minimap.SKYBOX_TIME_CHANGED, Arg.Any<JObject>());
            analytics.Received(1).Track(AnalyticsEvents.Minimap.SKYBOX_TIME_CHANGED, Arg.Is<JObject>(o => (float)o["time_of_day"]! == 0.5f));
        }

        [Test]
        public async Task StopTrackingAfterDispose()
        {
            eventBus.Publish(new MinimapEvents.SkyboxTimeChanged(0.5f));

            sut.Dispose();

            eventBus.Publish(new MinimapEvents.MapClicked());
            await UniTask.Delay(DEBOUNCE_WAIT_MS);

            analytics.DidNotReceiveWithAnyArgs().Track(default!);
        }
    }
}
