using DCL.Utilities;
using NUnit.Framework;
using System.Collections.Generic;

namespace DCL.VoiceChat.Nearby.Tests
{
    /// <summary>
    /// Documents the Nearby Voice Chat state machine behavior.
    ///
    /// States:
    ///   DISABLED  — feature is off, no audio processing
    ///   IDLE   — listening to nearby players, mic is muted
    ///   SPEAKING  — listening + mic is publishing to nearby players
    ///   SUPPRESSED — temporarily paused because a higher-priority call (Community/Private) is active
    ///
    /// Typical lifecycle:
    ///   DISABLED → Enable() → IDLE → StartSpeaking() → SPEAKING
    ///   → Suppress() → SUPPRESSED (via forced IDLE) → Resume() → IDLE
    ///   → Disable() → DISABLED
    ///
    /// Suppression rule: SPEAKING is always force-stopped on Suppress, so Resume returns to IDLE
    /// (or DISABLED if that was the user preference before suppression). This keeps the tear-down
    /// path uniform regardless of how speaking was triggered (widget toggle or push-to-talk) and
    /// requires the user to explicitly re-activate the mic after the higher-priority chat ends.
    /// </summary>
    public class NearbyVoiceChatStateModelShould
    {
        private NearbyVoiceChatStateModel model;
        private List<NearbyVoiceChatState> stateChanges;

        [SetUp]
        public void SetUp()
        {
            model = new NearbyVoiceChatStateModel(NearbyVoiceChatState.DISABLED);
            stateChanges = new List<NearbyVoiceChatState>();
            model.State.Subscribe(s => stateChanges.Add(s));
        }

        [TearDown]
        public void TearDown()
        {
            model.Dispose();
        }

        // ── Initialization ──────────────────────────────────────────

        [Test]
        public void StartInGivenInitialState()
        {
            Assert.That(model.State.Value, Is.EqualTo(NearbyVoiceChatState.DISABLED));
        }

        [Test]
        public void StartInSpeakingStateWhenInitializedAsSpeaking()
        {
            using var speakingModel = new NearbyVoiceChatStateModel(NearbyVoiceChatState.OPEN_MIC);
            Assert.That(speakingModel.State.Value, Is.EqualTo(NearbyVoiceChatState.OPEN_MIC));
        }

        // ── Enable / Disable ────────────────────────────────────────

        [Test]
        public void TransitionToHearingWhenEnabled()
        {
            // Act
            model.Enable();

            // Assert — player connects to island, starts hearing nearby voices
            Assert.That(model.State.Value, Is.EqualTo(NearbyVoiceChatState.IDLE));
        }

        [Test]
        public void IgnoreEnableWhenAlreadyActive()
        {
            // Arrange
            model.Enable();
            model.StartSpeaking();
            stateChanges.Clear();

            // Act — calling Enable while already speaking should not reset to IDLE
            model.Enable();

            // Assert
            Assert.That(model.State.Value, Is.EqualTo(NearbyVoiceChatState.OPEN_MIC));
            Assert.That(stateChanges, Is.Empty);
        }

        [Test]
        public void TransitionToDisabledFromAnyState(
            [Values(NearbyVoiceChatState.IDLE, NearbyVoiceChatState.OPEN_MIC)]
            NearbyVoiceChatState activeState)
        {
            // Arrange
            model.Enable();
            if (activeState == NearbyVoiceChatState.OPEN_MIC)
                model.StartSpeaking();

            // Act — player leaves the island or feature is toggled off
            model.Disable();

            // Assert
            Assert.That(model.State.Value, Is.EqualTo(NearbyVoiceChatState.DISABLED));
        }

        // ── Speaking ────────────────────────────────────────────────

        [Test]
        public void TransitionToSpeakingWhenMicActivated()
        {
            // Arrange — must be hearing first
            model.Enable();

            // Act — player presses PTT or toggles mic on
            model.StartSpeaking();

            // Assert
            Assert.That(model.State.Value, Is.EqualTo(NearbyVoiceChatState.OPEN_MIC));
        }

        [Test]
        public void NotStartSpeakingWhenDisabled()
        {
            // Act — trying to speak while feature is off
            model.StartSpeaking();

            // Assert — should stay DISABLED, speaking requires IDLE state first
            Assert.That(model.State.Value, Is.EqualTo(NearbyVoiceChatState.DISABLED));
        }

        [Test]
        public void NotStartSpeakingWhenSuppressed()
        {
            // Arrange
            model.Enable();
            model.Suppress(SuppressionReason.CALL);

            // Act — trying to speak while suppressed by another call
            model.StartSpeaking();

            // Assert
            Assert.That(model.State.Value, Is.EqualTo(NearbyVoiceChatState.SUPPRESSED));
        }

        [Test]
        public void TransitionBackToHearingWhenMicDeactivated()
        {
            // Arrange
            model.Enable();
            model.StartSpeaking();

            // Act — player releases PTT or toggles mic off
            model.StopSpeaking();

            // Assert — still hearing nearby players, just not transmitting
            Assert.That(model.State.Value, Is.EqualTo(NearbyVoiceChatState.IDLE));
        }

        [Test]
        public void IgnoreStopSpeakingWhenNotSpeaking()
        {
            // Arrange
            model.Enable();
            stateChanges.Clear();

            // Act
            model.StopSpeaking();

            // Assert
            Assert.That(model.State.Value, Is.EqualTo(NearbyVoiceChatState.IDLE));
            Assert.That(stateChanges, Is.Empty);
        }

        // ── Suppression (higher-priority call takes over) ───────────

        [Test]
        public void SuppressWhenHigherPriorityCallStarts()
        {
            // Arrange — player is hearing nearby voices
            model.Enable();

            // Act — player joins a Community or Private voice call
            model.Suppress(SuppressionReason.CALL);

            // Assert — nearby chat pauses, resources released
            Assert.That(model.State.Value, Is.EqualTo(NearbyVoiceChatState.SUPPRESSED));
        }

        [Test]
        public void SuppressFromSpeakingState()
        {
            // Arrange — player is actively speaking to nearby players
            model.Enable();
            model.StartSpeaking();

            // Act — incoming private call, nearby must yield
            model.Suppress(SuppressionReason.CALL);

            // Assert
            Assert.That(model.State.Value, Is.EqualTo(NearbyVoiceChatState.SUPPRESSED));
        }

        [Test]
        public void NotOverwritePreSuppressedStateOnDoubleSuppression()
        {
            // Arrange — player was speaking, then suppressed (force-stopped to IDLE before SUPPRESSED)
            model.Enable();
            model.StartSpeaking();
            model.Suppress(SuppressionReason.CALL);
            stateChanges.Clear();

            // Act — another suppression event (should be idempotent)
            model.Suppress(SuppressionReason.CALL);

            // Assert — no state change, pre-blocked state (IDLE) preserved
            Assert.That(stateChanges, Is.Empty);

            // Verify resume returns to IDLE (mic is not auto-restored — user must re-activate)
            model.Resume(SuppressionReason.CALL);
            Assert.That(model.State.Value, Is.EqualTo(NearbyVoiceChatState.IDLE));
        }

        [Test]
        public void ResumeToHearingAfterSuppression()
        {
            // Arrange — was hearing, then suppressed
            model.Enable();
            model.Suppress(SuppressionReason.CALL);

            // Act — higher-priority call ended
            model.Resume(SuppressionReason.CALL);

            // Assert — back to hearing nearby players
            Assert.That(model.State.Value, Is.EqualTo(NearbyVoiceChatState.IDLE));
        }

        [Test]
        public void ResumeToIdleAfterSuppressionFromSpeaking()
        {
            // Arrange — was speaking, then suppressed by incoming call
            model.Enable();
            model.StartSpeaking();
            model.Suppress(SuppressionReason.CALL);

            // Act — call ended, nearby resumes
            model.Resume(SuppressionReason.CALL);

            // Assert — mic is NOT auto-restored. The user must explicitly re-activate it:
            //   * for PTT: the release event can be missed during SUPPRESSED, so auto-restore would leak the mic;
            //   * for toggle: designers require the user to consciously opt back in after another chat ended.
            Assert.That(model.State.Value, Is.EqualTo(NearbyVoiceChatState.IDLE));
        }

        [Test]
        public void EmitStopSpeakingTransitionOnSuppressFromSpeaking()
        {
            // Arrange
            model.Enable();
            model.StartSpeaking();
            stateChanges.Clear();

            // Act — suppression arrives while SPEAKING
            model.Suppress(SuppressionReason.CALL);

            // Assert — SPEAKING → IDLE (forced stop) → SUPPRESSED, so downstream listeners
            // (e.g. mic publisher) tear down cleanly instead of only seeing SPEAKING → SUPPRESSED.
            Assert.That(stateChanges, Is.EqualTo(new[]
            {
                NearbyVoiceChatState.IDLE,
                NearbyVoiceChatState.SUPPRESSED,
            }));
        }

        [Test]
        public void ResumeToDisabledWhenUserPreferenceWasDisabled()
        {
            // Arrange — user has the feature disabled, then LOADING suppression kicks in on startup
            using var disabledModel = new NearbyVoiceChatStateModel(NearbyVoiceChatState.DISABLED);
            disabledModel.Suppress(SuppressionReason.LOADING);

            // Act — loading completes
            disabledModel.Resume(SuppressionReason.LOADING);

            // Assert — user preference preserved, feature does not silently turn itself on
            Assert.That(disabledModel.State.Value, Is.EqualTo(NearbyVoiceChatState.DISABLED));
        }

        [Test]
        public void IgnoreResumeWhenNotSuppressed()
        {
            // Arrange
            model.Enable();
            stateChanges.Clear();

            // Act — no-op when not suppressed
            model.Resume(SuppressionReason.CALL);

            // Assert
            Assert.That(model.State.Value, Is.EqualTo(NearbyVoiceChatState.IDLE));
            Assert.That(stateChanges, Is.Empty);
        }

        // ── Full Lifecycle ──────────────────────────────────────────

        [Test]
        public void SupportCompleteFeatureLifecycle()
        {
            // Player joins island → hears nearby players
            model.Enable();
            Assert.That(model.State.Value, Is.EqualTo(NearbyVoiceChatState.IDLE));

            // Player activates microphone → starts speaking to nearby players
            model.StartSpeaking();
            Assert.That(model.State.Value, Is.EqualTo(NearbyVoiceChatState.OPEN_MIC));

            // Player receives a private call → nearby suppressed (SPEAKING force-stopped to IDLE first)
            model.Suppress(SuppressionReason.CALL);
            Assert.That(model.State.Value, Is.EqualTo(NearbyVoiceChatState.SUPPRESSED));

            // Private call ends → nearby resumes to IDLE (user must re-activate mic explicitly)
            model.Resume(SuppressionReason.CALL);
            Assert.That(model.State.Value, Is.EqualTo(NearbyVoiceChatState.IDLE));

            // Player leaves island → feature disabled
            model.Disable();
            Assert.That(model.State.Value, Is.EqualTo(NearbyVoiceChatState.DISABLED));
        }

        // ── Reactive Notifications ──────────────────────────────────

        [Test]
        public void NotifySubscribersOnEveryStateTransition()
        {
            // Act
            model.Enable();
            model.StartSpeaking();
            model.Suppress(SuppressionReason.CALL);
            model.Resume(SuppressionReason.CALL);
            model.Disable();

            // Assert — every meaningful transition was observed
            Assert.That(stateChanges, Is.EqualTo(new[]
            {
                NearbyVoiceChatState.IDLE,       // Enable
                NearbyVoiceChatState.OPEN_MIC,   // StartSpeaking
                NearbyVoiceChatState.IDLE,       // Suppress — force-stop SPEAKING
                NearbyVoiceChatState.SUPPRESSED, // Suppress — enter suppression
                NearbyVoiceChatState.IDLE,       // Resume — user preference was IDLE
                NearbyVoiceChatState.DISABLED,   // Disable
            }));
        }

        [Test]
        public void NotNotifySubscribersOnNoOpTransitions()
        {
            // Arrange
            model.Enable();
            stateChanges.Clear();

            // Act — all no-ops: Enable while active, StopSpeaking while hearing, Resume while not suppressed
            model.Enable();
            model.StopSpeaking();
            model.Resume(SuppressionReason.CALL);

            // Assert
            Assert.That(stateChanges, Is.Empty);
        }

        // ── IsOpenMic synthetic reactive ────────────────────────────

        [Test]
        public void StartIsOpenMicFalseWhenInitialStateIsNotOpenMic()
        {
            // Assert — model was constructed with DISABLED in SetUp
            Assert.That(model.IsOpenMic.Value, Is.False);
        }

        [Test]
        public void StartIsOpenMicTrueWhenInitialStateIsOpenMic()
        {
            // Arrange + Act
            using var speakingModel = new NearbyVoiceChatStateModel(NearbyVoiceChatState.OPEN_MIC);

            // Assert
            Assert.That(speakingModel.IsOpenMic.Value, Is.True);
        }

        [Test]
        public void FlipIsOpenMicTrueOnStartSpeaking()
        {
            // Arrange
            model.Enable();

            // Act
            model.StartSpeaking();

            // Assert
            Assert.That(model.IsOpenMic.Value, Is.True);
        }

        [Test]
        public void FlipIsOpenMicFalseOnStopSpeaking()
        {
            // Arrange
            model.Enable();
            model.StartSpeaking();

            // Act
            model.StopSpeaking();

            // Assert
            Assert.That(model.IsOpenMic.Value, Is.False);
        }

        [Test]
        public void FlipIsOpenMicFalseWhenSuppressedFromOpenMic()
        {
            // Arrange — speaking, then suppression cascade (OPEN_MIC → IDLE → SUPPRESSED)
            model.Enable();
            model.StartSpeaking();

            // Act
            model.Suppress(SuppressionReason.CALL);

            // Assert — both intermediate transitions land IsOpenMic at false
            Assert.That(model.IsOpenMic.Value, Is.False);
        }

        [Test]
        public void NotChangeIsOpenMicOnSuppressFromIdle()
        {
            // Arrange — not speaking
            model.Enable();
            var openMicChanges = new List<bool>();
            model.IsOpenMic.Subscribe(v => openMicChanges.Add(v));

            // Act
            model.Suppress(SuppressionReason.CALL);

            // Assert — never went true, never emitted
            Assert.That(model.IsOpenMic.Value, Is.False);
            Assert.That(openMicChanges, Is.Empty);
        }

        [Test]
        public void NotChangeIsOpenMicOnResumeToIdle()
        {
            // Arrange — was speaking, suppression force-stopped it. After Resume we return to IDLE, IsOpenMic stays false.
            model.Enable();
            model.StartSpeaking();
            model.Suppress(SuppressionReason.CALL);
            var openMicChanges = new List<bool>();
            model.IsOpenMic.Subscribe(v => openMicChanges.Add(v));

            // Act
            model.Resume(SuppressionReason.CALL);

            // Assert
            Assert.That(model.IsOpenMic.Value, Is.False);
            Assert.That(openMicChanges, Is.Empty);
        }

        [Test]
        public void EmitSingleStopTransitionOnSuppressFromOpenMic()
        {
            // Arrange
            model.Enable();
            model.StartSpeaking();
            var openMicChanges = new List<bool>();
            model.IsOpenMic.Subscribe(v => openMicChanges.Add(v));

            // Act — OPEN_MIC → IDLE → SUPPRESSED
            model.Suppress(SuppressionReason.CALL);

            // Assert — only the OPEN_MIC → IDLE transition flips IsOpenMic;
            // the IDLE → SUPPRESSED hop is already false and is filtered by ReactiveProperty equality.
            Assert.That(openMicChanges, Is.EqualTo(new[] { false }));
        }

        [Test]
        public void EmitSingleStartTransitionOnStartSpeaking()
        {
            // Arrange
            model.Enable();
            var openMicChanges = new List<bool>();
            model.IsOpenMic.Subscribe(v => openMicChanges.Add(v));

            // Act
            model.StartSpeaking();

            // Assert
            Assert.That(openMicChanges, Is.EqualTo(new[] { true }));
        }

        [Test]
        public void NotEmitIsOpenMicWhenDisabledFromIdle()
        {
            // Arrange — never speaking
            model.Enable();
            var openMicChanges = new List<bool>();
            model.IsOpenMic.Subscribe(v => openMicChanges.Add(v));

            // Act
            model.Disable();

            // Assert — IDLE → DISABLED, IsOpenMic stays false throughout
            Assert.That(openMicChanges, Is.Empty);
        }
    }
}
