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
    ///   → Suppress() → SUPPRESSED → Resume() → SPEAKING
    ///   → StopSpeaking() → IDLE → Disable() → DISABLED
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
            using var speakingModel = new NearbyVoiceChatStateModel(NearbyVoiceChatState.SPEAKING);
            Assert.That(speakingModel.State.Value, Is.EqualTo(NearbyVoiceChatState.SPEAKING));
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
            Assert.That(model.State.Value, Is.EqualTo(NearbyVoiceChatState.SPEAKING));
            Assert.That(stateChanges, Is.Empty);
        }

        [Test]
        public void TransitionToDisabledFromAnyState(
            [Values(NearbyVoiceChatState.IDLE, NearbyVoiceChatState.SPEAKING)]
            NearbyVoiceChatState activeState)
        {
            // Arrange
            model.Enable();
            if (activeState == NearbyVoiceChatState.SPEAKING)
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
            Assert.That(model.State.Value, Is.EqualTo(NearbyVoiceChatState.SPEAKING));
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
            // Arrange — player was speaking, then suppressed
            model.Enable();
            model.StartSpeaking();
            model.Suppress(SuppressionReason.CALL);
            stateChanges.Clear();

            // Act — another suppression event (should be idempotent)
            model.Suppress(SuppressionReason.CALL);

            // Assert — no state change, pre-blocked state (SPEAKING) preserved
            Assert.That(stateChanges, Is.Empty);

            // Verify resume still returns to SPEAKING (the original pre-blocked state)
            model.Resume(SuppressionReason.CALL);
            Assert.That(model.State.Value, Is.EqualTo(NearbyVoiceChatState.SPEAKING));
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
        public void ResumeToSpeakingAfterSuppression()
        {
            // Arrange — was speaking, then suppressed by incoming call
            model.Enable();
            model.StartSpeaking();
            model.Suppress(SuppressionReason.CALL);

            // Act — call ended, nearby resumes
            model.Resume(SuppressionReason.CALL);

            // Assert — mic restored to active state
            Assert.That(model.State.Value, Is.EqualTo(NearbyVoiceChatState.SPEAKING));
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
            Assert.That(model.State.Value, Is.EqualTo(NearbyVoiceChatState.SPEAKING));

            // Player receives a private call → nearby suppressed
            model.Suppress(SuppressionReason.CALL);
            Assert.That(model.State.Value, Is.EqualTo(NearbyVoiceChatState.SUPPRESSED));

            // Private call ends → nearby resumes with mic still on
            model.Resume(SuppressionReason.CALL);
            Assert.That(model.State.Value, Is.EqualTo(NearbyVoiceChatState.SPEAKING));

            // Player deactivates microphone → back to hearing only
            model.StopSpeaking();
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
            model.StopSpeaking();
            model.Disable();

            // Assert — every meaningful transition was observed
            Assert.That(stateChanges, Is.EqualTo(new[]
            {
                NearbyVoiceChatState.IDLE,   // Enable
                NearbyVoiceChatState.SPEAKING,   // StartSpeaking
                NearbyVoiceChatState.SUPPRESSED, // Suppress
                NearbyVoiceChatState.SPEAKING,   // Resume (back to pre-blocked)
                NearbyVoiceChatState.IDLE,    // StopSpeaking
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
    }
}
