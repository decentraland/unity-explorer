using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.PerformanceAndDiagnostics.Analytics.EventBased;
using DCL.VoiceChat.Nearby.MutePersistence;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NUnit.Framework;
using System;

namespace DCL.VoiceChat.Nearby.Tests
{
    /// <summary>
    /// Guards the edge cases called out in the Nearby Voice Chat analytics PR:
    /// events must fire only on user-driven transitions. System-driven transitions
    /// (focus-resume, suppression by call/scene/loading) must be filtered out so that
    /// per-session usage metrics are not inflated by automatic state changes.
    /// </summary>
    [TestFixture]
    public class NearbyVoiceChatAnalyticsShould
    {
        private const string WALLET = "0xABC";
        private const string OTHER_WALLET = "0xDEF";

        private IAnalyticsController analytics = null!;
        private NearbyVoiceChatStateModel stateModel = null!;
        private INearbyMuteCache muteCache = null!;
        private NearbyMuteService muteService = null!;
        private NearbyVoiceChatAnalytics sut = null!;

        [SetUp]
        public void SetUp()
        {
            analytics = Substitute.For<IAnalyticsController>();
            muteCache = Substitute.For<INearbyMuteCache>();

            // The repository is unused by the analytics layer; the service just needs a non-null collaborator.
            INearbyMuteRepository repository = Substitute.For<INearbyMuteRepository>();
            muteService = new NearbyMuteService(muteCache, repository);

            stateModel = new NearbyVoiceChatStateModel(NearbyVoiceChatState.DISABLED);
            sut = new NearbyVoiceChatAnalytics(analytics, stateModel, muteService);
        }

        [TearDown]
        public void TearDown()
        {
            sut.Dispose();
            stateModel.Dispose();
        }

        // ── nearby_voice_speak ──────────────────────────────────────

        [Test]
        public void FireSpeakEventOnIdleToSpeakingWithButton()
        {
            // Arrange
            stateModel.Enable();
            analytics.ClearReceivedCalls();

            // Act — user clicks the widget speak button
            stateModel.StartSpeaking(NearbyVoiceActivation.BUTTON);

            // Assert
            AssertTrackedOnce(AnalyticsEvents.VoiceChat.NEARBY_VOICE_SPEAK,
                p => (string?)p["activation"] == "button");
        }

        [Test]
        public void FireSpeakEventOnIdleToSpeakingWithPushToTalk()
        {
            // Arrange
            stateModel.Enable();
            analytics.ClearReceivedCalls();

            // Act — user holds [T]
            stateModel.StartSpeaking(NearbyVoiceActivation.PUSH_TO_TALK);

            // Assert
            AssertTrackedOnce(AnalyticsEvents.VoiceChat.NEARBY_VOICE_SPEAK,
                p => (string?)p["activation"] == "push_to_talk");
        }

        [Test]
        public void NotFireSpeakEventOnFocusResumed()
        {
            // Focus-resume is a continuation of a prior speak session, not a fresh use — filter it out.
            // Arrange
            stateModel.Enable();
            analytics.ClearReceivedCalls();

            // Act — application regained focus and the mic auto-resumed
            stateModel.StartSpeaking(NearbyVoiceActivation.FOCUS_RESUMED);

            // Assert
            analytics.DidNotReceive().Track(
                AnalyticsEvents.VoiceChat.NEARBY_VOICE_SPEAK,
                Arg.Any<JObject>(),
                Arg.Any<bool>());
        }

        [Test]
        public void FireSpeakEventPerFreshSpeakingSession()
        {
            // Arrange
            stateModel.Enable();
            analytics.ClearReceivedCalls();

            // Act — two separate speak sessions, different activations
            stateModel.StartSpeaking(NearbyVoiceActivation.BUTTON);
            stateModel.StopSpeaking();
            stateModel.StartSpeaking(NearbyVoiceActivation.PUSH_TO_TALK);

            // Assert — one event per fresh IDLE → SPEAKING entry
            analytics.Received(2).Track(
                AnalyticsEvents.VoiceChat.NEARBY_VOICE_SPEAK,
                Arg.Any<JObject>(),
                Arg.Any<bool>());
        }

        // ── nearby_voice_toggle (user-driven on/off via HearOthersToggle) ───

        [Test]
        public void FireToggleOnWhenEnabledFromDisabled()
        {
            // Act — user toggles the widget on from DISABLED (e.g. first session after preference was off)
            stateModel.Enable();

            // Assert
            AssertTrackedOnce(AnalyticsEvents.VoiceChat.NEARBY_VOICE_TOGGLE,
                p => (bool?)p["enabled"] == true);
        }

        [Test]
        public void FireToggleOffFromIdle()
        {
            // Arrange
            stateModel.Enable();
            analytics.ClearReceivedCalls();

            // Act — user toggles the widget off while listening
            stateModel.Disable();

            // Assert
            AssertTrackedOnce(AnalyticsEvents.VoiceChat.NEARBY_VOICE_TOGGLE,
                p => (bool?)p["enabled"] == false);
        }

        [Test]
        public void FireToggleOffFromSpeaking()
        {
            // SPEAKING → DISABLED happens if the user toggles the widget off mid-speak.
            // Arrange
            stateModel.Enable();
            stateModel.StartSpeaking(NearbyVoiceActivation.BUTTON);
            analytics.ClearReceivedCalls();

            // Act
            stateModel.Disable();

            // Assert
            AssertTrackedOnce(AnalyticsEvents.VoiceChat.NEARBY_VOICE_TOGGLE,
                p => (bool?)p["enabled"] == false);
        }

        [Test]
        public void NotFireToggleOnSuppressionFromIdle()
        {
            // Incoming call / scene restriction / initial loading — system-driven, not user intent.
            // Arrange
            stateModel.Enable();
            analytics.ClearReceivedCalls();

            // Act
            stateModel.Suppress(SuppressionReason.CALL);

            // Assert
            AssertNoToggle();
        }

        [Test]
        public void NotFireToggleOnSuppressionFromSpeaking()
        {
            // Suppress from SPEAKING emits SPEAKING → IDLE → SUPPRESSED.
            // The intermediate IDLE is a forced stop, not a user-driven toggle.
            // Arrange
            stateModel.Enable();
            stateModel.StartSpeaking(NearbyVoiceActivation.BUTTON);
            analytics.ClearReceivedCalls();

            // Act
            stateModel.Suppress(SuppressionReason.CALL);

            // Assert
            AssertNoToggle();
        }

        [Test]
        public void NotFireToggleOnResumeToIdle()
        {
            // Higher-priority call ended → nearby chat resumes to IDLE. Not a user toggle.
            // Arrange
            stateModel.Enable();
            stateModel.Suppress(SuppressionReason.CALL);
            analytics.ClearReceivedCalls();

            // Act
            stateModel.Resume(SuppressionReason.CALL);

            // Assert
            AssertNoToggle();
        }

        [Test]
        public void NotFireToggleOnResumeToDisabled()
        {
            // Loading stage suppresses the feature at startup while the user preference is DISABLED.
            // After load completes, Resume restores the preference — no toggle event should fire.
            // Arrange
            using var disabledStart = new NearbyVoiceChatStateModel(NearbyVoiceChatState.DISABLED);
            using var localAnalytics = new NearbyVoiceChatAnalytics(analytics, disabledStart, muteService);
            disabledStart.Suppress(SuppressionReason.LOADING);
            analytics.ClearReceivedCalls();

            // Act
            disabledStart.Resume(SuppressionReason.LOADING);

            // Assert
            AssertNoToggle();
        }

        [Test]
        public void NotFireToggleOnStopSpeaking()
        {
            // SPEAKING → IDLE via StopSpeaking is mic-off, not a feature toggle.
            // Arrange
            stateModel.Enable();
            stateModel.StartSpeaking(NearbyVoiceActivation.BUTTON);
            analytics.ClearReceivedCalls();

            // Act
            stateModel.StopSpeaking();

            // Assert
            AssertNoToggle();
        }

        [Test]
        public void NotFireToggleOnStartSpeaking()
        {
            // IDLE → SPEAKING emits a speak event, never a toggle.
            // Arrange
            stateModel.Enable();
            analytics.ClearReceivedCalls();

            // Act
            stateModel.StartSpeaking(NearbyVoiceActivation.BUTTON);

            // Assert
            AssertNoToggle();
        }

        [Test]
        public void NotFireToggleOnDisableFromSuppressed()
        {
            // User is inside a private call (nearby is SUPPRESSED) and disables the widget via settings.
            // Current implementation only tracks toggle-off from IDLE/SPEAKING; SUPPRESSED transitions are skipped
            // since the user was not actually using nearby at the moment.
            // Arrange
            stateModel.Enable();
            stateModel.Suppress(SuppressionReason.CALL);
            analytics.ClearReceivedCalls();

            // Act
            stateModel.Disable();

            // Assert
            AssertNoToggle();
        }

        // ── nearby_voice_user_mute ──────────────────────────────────

        [Test]
        public void FireUserMuteEventOnMute()
        {
            // Act
            RaiseMuteEvent(WALLET, true);

            // Assert
            AssertTrackedOnce(AnalyticsEvents.VoiceChat.NEARBY_VOICE_USER_MUTE,
                p => (string?)p["identity"] == WALLET && (bool?)p["is_muted"] == true);
        }

        [Test]
        public void FireUserMuteEventOnUnmute()
        {
            // Act
            RaiseMuteEvent(WALLET, false);

            // Assert
            AssertTrackedOnce(AnalyticsEvents.VoiceChat.NEARBY_VOICE_USER_MUTE,
                p => (string?)p["identity"] == WALLET && (bool?)p["is_muted"] == false);
        }

        [Test]
        public void EmitOneMuteEventPerCacheTransition()
        {
            // The cache de-dupes duplicate mutes, so the analytics layer should see one event
            // per real state change — nothing more, nothing less.
            // Act
            RaiseMuteEvent(WALLET, true);
            RaiseMuteEvent(OTHER_WALLET, true);
            RaiseMuteEvent(WALLET, false);

            // Assert
            analytics.Received(3).Track(
                AnalyticsEvents.VoiceChat.NEARBY_VOICE_USER_MUTE,
                Arg.Any<JObject>(),
                Arg.Any<bool>());
        }

        // ── Dispose ─────────────────────────────────────────────────

        [Test]
        public void NotFireSpeakAfterDispose()
        {
            // Arrange
            stateModel.Enable();
            analytics.ClearReceivedCalls();
            sut.Dispose();

            // Act
            stateModel.StartSpeaking(NearbyVoiceActivation.BUTTON);

            // Assert
            analytics.DidNotReceive().Track(
                AnalyticsEvents.VoiceChat.NEARBY_VOICE_SPEAK,
                Arg.Any<JObject>(),
                Arg.Any<bool>());
        }

        [Test]
        public void NotFireToggleAfterDispose()
        {
            // Arrange
            stateModel.Enable();
            analytics.ClearReceivedCalls();
            sut.Dispose();

            // Act
            stateModel.Disable();

            // Assert
            AssertNoToggle();
        }

        [Test]
        public void NotFireMuteAfterDispose()
        {
            // Arrange
            sut.Dispose();

            // Act
            RaiseMuteEvent(WALLET, true);

            // Assert
            analytics.DidNotReceive().Track(
                AnalyticsEvents.VoiceChat.NEARBY_VOICE_USER_MUTE,
                Arg.Any<JObject>(),
                Arg.Any<bool>());
        }

        // ── Helpers ─────────────────────────────────────────────────

        private void AssertTrackedOnce(string eventName, Func<JObject, bool> payloadMatch)
        {
            analytics.Received(1).Track(
                eventName,
                Arg.Is<JObject>(p => p != null && payloadMatch(p)),
                Arg.Any<bool>());
        }

        private void AssertNoToggle()
        {
            analytics.DidNotReceive().Track(
                AnalyticsEvents.VoiceChat.NEARBY_VOICE_TOGGLE,
                Arg.Any<JObject>(),
                Arg.Any<bool>());
        }

        private void RaiseMuteEvent(string wallet, bool isMuted)
        {
            muteCache.MuteStateChanged += Raise.Event<Action<string, bool>>(wallet, isMuted);
        }
    }
}
