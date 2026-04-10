using DCL.Utilities;
using NUnit.Framework;
using System.Collections.Generic;

namespace DCL.VoiceChat.Proximity.Tests
{
    /// <summary>
    /// Documents the Proximity Voice Chat state machine behavior.
    ///
    /// States:
    ///   DISABLED  — feature is off, no audio processing
    ///   HEARING   — listening to nearby players, mic is muted
    ///   SPEAKING  — listening + mic is publishing to nearby players
    ///   SUPPRESSED — temporarily paused because a higher-priority call (Community/Private) is active
    ///
    /// Typical lifecycle:
    ///   DISABLED → Enable() → HEARING → StartSpeaking() → SPEAKING
    ///   → Suppress() → SUPPRESSED → Resume() → SPEAKING
    ///   → StopSpeaking() → HEARING → Disable() → DISABLED
    /// </summary>
    public class ProximityVoiceChatStateModelShould
    {
        private ProximityVoiceChatStateModel model;
        private List<ProximityVoiceChatState> stateChanges;

        [SetUp]
        public void SetUp()
        {
            model = new ProximityVoiceChatStateModel(ProximityVoiceChatState.DISABLED);
            stateChanges = new List<ProximityVoiceChatState>();
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
            Assert.That(model.State.Value, Is.EqualTo(ProximityVoiceChatState.DISABLED));
        }

        [Test]
        public void StartInSpeakingStateWhenInitializedAsSpeaking()
        {
            using var speakingModel = new ProximityVoiceChatStateModel(ProximityVoiceChatState.SPEAKING);
            Assert.That(speakingModel.State.Value, Is.EqualTo(ProximityVoiceChatState.SPEAKING));
        }

        // ── Enable / Disable ────────────────────────────────────────

        [Test]
        public void TransitionToHearingWhenEnabled()
        {
            // Act
            model.Enable();

            // Assert — player connects to island, starts hearing nearby voices
            Assert.That(model.State.Value, Is.EqualTo(ProximityVoiceChatState.HEARING));
        }

        [Test]
        public void IgnoreEnableWhenAlreadyActive()
        {
            // Arrange
            model.Enable();
            model.StartSpeaking();
            stateChanges.Clear();

            // Act — calling Enable while already speaking should not reset to HEARING
            model.Enable();

            // Assert
            Assert.That(model.State.Value, Is.EqualTo(ProximityVoiceChatState.SPEAKING));
            Assert.That(stateChanges, Is.Empty);
        }

        [Test]
        public void TransitionToDisabledFromAnyState(
            [Values(ProximityVoiceChatState.HEARING, ProximityVoiceChatState.SPEAKING)]
            ProximityVoiceChatState activeState)
        {
            // Arrange
            model.Enable();
            if (activeState == ProximityVoiceChatState.SPEAKING)
                model.StartSpeaking();

            // Act — player leaves the island or feature is toggled off
            model.Disable();

            // Assert
            Assert.That(model.State.Value, Is.EqualTo(ProximityVoiceChatState.DISABLED));
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
            Assert.That(model.State.Value, Is.EqualTo(ProximityVoiceChatState.SPEAKING));
        }

        [Test]
        public void NotStartSpeakingWhenDisabled()
        {
            // Act — trying to speak while feature is off
            model.StartSpeaking();

            // Assert — should stay DISABLED, speaking requires HEARING state first
            Assert.That(model.State.Value, Is.EqualTo(ProximityVoiceChatState.DISABLED));
        }

        [Test]
        public void NotStartSpeakingWhenSuppressed()
        {
            // Arrange
            model.Enable();
            model.Suppress();

            // Act — trying to speak while suppressed by another call
            model.StartSpeaking();

            // Assert
            Assert.That(model.State.Value, Is.EqualTo(ProximityVoiceChatState.SUPPRESSED));
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
            Assert.That(model.State.Value, Is.EqualTo(ProximityVoiceChatState.HEARING));
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
            Assert.That(model.State.Value, Is.EqualTo(ProximityVoiceChatState.HEARING));
            Assert.That(stateChanges, Is.Empty);
        }

        // ── Suppression (higher-priority call takes over) ───────────

        [Test]
        public void SuppressWhenHigherPriorityCallStarts()
        {
            // Arrange — player is hearing nearby voices
            model.Enable();

            // Act — player joins a Community or Private voice call
            model.Suppress();

            // Assert — proximity chat pauses, resources released
            Assert.That(model.State.Value, Is.EqualTo(ProximityVoiceChatState.SUPPRESSED));
        }

        [Test]
        public void SuppressFromSpeakingState()
        {
            // Arrange — player is actively speaking to nearby players
            model.Enable();
            model.StartSpeaking();

            // Act — incoming private call, proximity must yield
            model.Suppress();

            // Assert
            Assert.That(model.State.Value, Is.EqualTo(ProximityVoiceChatState.SUPPRESSED));
        }

        [Test]
        public void NotOverwritePreSuppressedStateOnDoubleSuppression()
        {
            // Arrange — player was speaking, then suppressed
            model.Enable();
            model.StartSpeaking();
            model.Suppress();
            stateChanges.Clear();

            // Act — another suppression event (should be idempotent)
            model.Suppress();

            // Assert — no state change, pre-blocked state (SPEAKING) preserved
            Assert.That(stateChanges, Is.Empty);

            // Verify resume still returns to SPEAKING (the original pre-blocked state)
            model.Resume();
            Assert.That(model.State.Value, Is.EqualTo(ProximityVoiceChatState.SPEAKING));
        }

        [Test]
        public void ResumeToHearingAfterSuppression()
        {
            // Arrange — was hearing, then suppressed
            model.Enable();
            model.Suppress();

            // Act — higher-priority call ended
            model.Resume();

            // Assert — back to hearing nearby players
            Assert.That(model.State.Value, Is.EqualTo(ProximityVoiceChatState.HEARING));
        }

        [Test]
        public void ResumeToSpeakingAfterSuppression()
        {
            // Arrange — was speaking, then suppressed by incoming call
            model.Enable();
            model.StartSpeaking();
            model.Suppress();

            // Act — call ended, proximity resumes
            model.Resume();

            // Assert — mic restored to active state
            Assert.That(model.State.Value, Is.EqualTo(ProximityVoiceChatState.SPEAKING));
        }

        [Test]
        public void IgnoreResumeWhenNotSuppressed()
        {
            // Arrange
            model.Enable();
            stateChanges.Clear();

            // Act — no-op when not suppressed
            model.Resume();

            // Assert
            Assert.That(model.State.Value, Is.EqualTo(ProximityVoiceChatState.HEARING));
            Assert.That(stateChanges, Is.Empty);
        }

        // ── Full Lifecycle ──────────────────────────────────────────

        [Test]
        public void SupportCompleteFeatureLifecycle()
        {
            // Player joins island → hears nearby players
            model.Enable();
            Assert.That(model.State.Value, Is.EqualTo(ProximityVoiceChatState.HEARING));

            // Player activates microphone → starts speaking to nearby players
            model.StartSpeaking();
            Assert.That(model.State.Value, Is.EqualTo(ProximityVoiceChatState.SPEAKING));

            // Player receives a private call → proximity suppressed
            model.Suppress();
            Assert.That(model.State.Value, Is.EqualTo(ProximityVoiceChatState.SUPPRESSED));

            // Private call ends → proximity resumes with mic still on
            model.Resume();
            Assert.That(model.State.Value, Is.EqualTo(ProximityVoiceChatState.SPEAKING));

            // Player deactivates microphone → back to hearing only
            model.StopSpeaking();
            Assert.That(model.State.Value, Is.EqualTo(ProximityVoiceChatState.HEARING));

            // Player leaves island → feature disabled
            model.Disable();
            Assert.That(model.State.Value, Is.EqualTo(ProximityVoiceChatState.DISABLED));
        }

        // ── Reactive Notifications ──────────────────────────────────

        [Test]
        public void NotifySubscribersOnEveryStateTransition()
        {
            // Act
            model.Enable();
            model.StartSpeaking();
            model.Suppress();
            model.Resume();
            model.StopSpeaking();
            model.Disable();

            // Assert — every meaningful transition was observed
            Assert.That(stateChanges, Is.EqualTo(new[]
            {
                ProximityVoiceChatState.HEARING,   // Enable
                ProximityVoiceChatState.SPEAKING,   // StartSpeaking
                ProximityVoiceChatState.SUPPRESSED, // Suppress
                ProximityVoiceChatState.SPEAKING,   // Resume (back to pre-blocked)
                ProximityVoiceChatState.HEARING,    // StopSpeaking
                ProximityVoiceChatState.DISABLED,   // Disable
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
            model.Resume();

            // Assert
            Assert.That(stateChanges, Is.Empty);
        }
    }
}
