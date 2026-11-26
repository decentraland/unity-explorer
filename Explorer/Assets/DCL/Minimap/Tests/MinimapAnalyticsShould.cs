using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.PerformanceAndDiagnostics.Analytics.EventBased;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NUnit.Framework;
using Utility;

namespace DCL.Minimap.Tests
{
    [TestFixture]
    public class MinimapAnalyticsShould
    {
        private IAnalyticsController analytics = null!;
        private IEventBus eventBus = null!;
        private MinimapAnalytics sut = null!;

        [SetUp]
        public void SetUp()
        {
            analytics = Substitute.For<IAnalyticsController>();
            eventBus = new EventBus(invokeSubscribersOnMainThread: false);
            sut = new MinimapAnalytics(analytics, eventBus);
        }

        [TearDown]
        public void TearDown() =>
            sut.Dispose();

        [TestCase(MinimapEvents.Action.Expanded, AnalyticsEvents.Minimap.EXPANDED)]
        [TestCase(MinimapEvents.Action.Collapsed, AnalyticsEvents.Minimap.COLLAPSED)]
        [TestCase(MinimapEvents.Action.MapClicked, AnalyticsEvents.Minimap.MAP_CLICKED)]
        [TestCase(MinimapEvents.Action.ContextMenuOpened, AnalyticsEvents.Minimap.CONTEXT_MENU_OPENED)]
        public void TrackActionsWithoutPayload(MinimapEvents.Action action, string expectedEvent)
        {
            eventBus.Publish(new MinimapEvents.Interaction(action));

            analytics.Received(1).Track(expectedEvent);
        }

        [TestCase(MinimapEvents.Action.FavoriteClicked, AnalyticsEvents.Minimap.FAVORITE_CLICKED, "is_favorite")]
        [TestCase(MinimapEvents.Action.SkyboxWidgetToggled, AnalyticsEvents.Minimap.SKYBOX_WIDGET_TOGGLED, "is_open")]
        [TestCase(MinimapEvents.Action.SkyboxDayCycleToggled, AnalyticsEvents.Minimap.SKYBOX_DAY_CYCLE_TOGGLED, "is_enabled")]
        public void TrackStateOfBooleanActions(MinimapEvents.Action action, string expectedEvent, string propertyName)
        {
            eventBus.Publish(new MinimapEvents.Interaction(action, state: true));

            analytics.Received(1).Track(expectedEvent, Arg.Is<JObject>(o => (bool)o[propertyName]!));
        }

        [Test]
        public void TrackTimeOfDayWhenSkyboxTimeChanges()
        {
            eventBus.Publish(new MinimapEvents.Interaction(MinimapEvents.Action.SkyboxTimeChanged, timeOfDay: 0.5f));

            analytics.Received(1).Track(AnalyticsEvents.Minimap.SKYBOX_TIME_CHANGED, Arg.Is<JObject>(o => (float)o["time_of_day"]! == 0.5f));
        }

        [Test]
        public void StopTrackingAfterDispose()
        {
            sut.Dispose();

            eventBus.Publish(new MinimapEvents.Interaction(MinimapEvents.Action.MapClicked));

            analytics.DidNotReceiveWithAnyArgs().Track(default!);
        }
    }
}
