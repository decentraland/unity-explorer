using DCL.Utilities;
using DCL.VoiceChat.Nearby;
using NUnit.Framework;
using UnityEngine;

namespace DCL.VoiceChat.Tests
{
    /// <summary>
    ///     Documents <see cref="NearbySpeakingAudibilityGate"/> as a per-activation filter on
    ///     <see cref="NearbyVoiceChatStateModel.IsOpenMic"/> driven by <see cref="VoiceChatConfiguration.nearbyPlaySfxOnPushToTalk"/>.
    /// </summary>
    public class NearbySpeakingAudibilityGateShould
    {
        private NearbyVoiceChatStateModel stateModel = null!;
        private VoiceChatConfiguration configuration = null!;
        private NearbySpeakingAudibilityGate gate = null!;

        [SetUp]
        public void SetUp()
        {
            stateModel = new NearbyVoiceChatStateModel(NearbyVoiceChatState.IDLE);
            configuration = ScriptableObject.CreateInstance<VoiceChatConfiguration>();
            configuration.nearbyPlaySfxOnPushToTalk = false;
            gate = new NearbySpeakingAudibilityGate(stateModel, configuration);
        }

        [TearDown]
        public void TearDown()
        {
            gate.Dispose();
            stateModel.Dispose();
            Object.DestroyImmediate(configuration);
        }

        [Test]
        public void StartEffectiveOpenMicFalse()
        {
            Assert.That(gate.EffectiveOpenMic.Value, Is.False);
        }

        [Test]
        public void EmitTrueWhenButtonActivation()
        {
            // Act
            stateModel.StartSpeaking(NearbyVoiceActivation.BUTTON);

            // Assert
            Assert.That(gate.EffectiveOpenMic.Value, Is.True);
        }

        [Test]
        public void SuppressTrueWhenPushToTalkAndFlagOff()
        {
            // Act
            stateModel.StartSpeaking(NearbyVoiceActivation.PUSH_TO_TALK);

            // Assert
            Assert.That(gate.EffectiveOpenMic.Value, Is.False);
        }

        [Test]
        public void EmitTrueWhenPushToTalkAndFlagOn()
        {
            // Arrange
            configuration.nearbyPlaySfxOnPushToTalk = true;

            // Act
            stateModel.StartSpeaking(NearbyVoiceActivation.PUSH_TO_TALK);

            // Assert
            Assert.That(gate.EffectiveOpenMic.Value, Is.True);
        }

        [Test]
        public void SymmetricallySuppressStopWhenStartWasSuppressed()
        {
            // Arrange — PTT start is muted (flag off)
            stateModel.StartSpeaking(NearbyVoiceActivation.PUSH_TO_TALK);
            Assert.That(gate.EffectiveOpenMic.Value, Is.False);

            // Act — stop with activation still being PTT
            stateModel.StopSpeaking();

            // Assert — gate stayed false, so MicrophoneAudioToggleHandler will not flip and will not play stop SFX
            Assert.That(gate.EffectiveOpenMic.Value, Is.False);
        }

        [Test]
        public void FlipBothEdgesWhenButtonActivation()
        {
            int trueEdges = 0;
            int falseEdges = 0;

            using var subscription = gate.EffectiveOpenMic.Subscribe(v =>
            {
                if (v) trueEdges++;
                else falseEdges++;
            });

            // Act
            stateModel.StartSpeaking(NearbyVoiceActivation.BUTTON);
            stateModel.StopSpeaking();

            // Assert — exactly one true and one false edge
            Assert.That(trueEdges, Is.EqualTo(1));
            Assert.That(falseEdges, Is.EqualTo(1));
        }

        [Test]
        public void NotRetroactivelyFlipWhenFlagToggledMidPushToTalkSession()
        {
            // Arrange — PTT start muted (flag off at the transition moment)
            stateModel.StartSpeaking(NearbyVoiceActivation.PUSH_TO_TALK);
            Assert.That(gate.EffectiveOpenMic.Value, Is.False);

            // Act — designer flips flag mid-session; nothing should change until the next transition
            configuration.nearbyPlaySfxOnPushToTalk = true;

            // Assert — still false (gate only samples at transition time)
            Assert.That(gate.EffectiveOpenMic.Value, Is.False);
        }

        [Test]
        public void StopReactingAfterDispose()
        {
            // Arrange
            gate.Dispose();

            // Act
            stateModel.StartSpeaking(NearbyVoiceActivation.BUTTON);

            // Assert
            Assert.That(gate.EffectiveOpenMic.Value, Is.False);
        }
    }
}
