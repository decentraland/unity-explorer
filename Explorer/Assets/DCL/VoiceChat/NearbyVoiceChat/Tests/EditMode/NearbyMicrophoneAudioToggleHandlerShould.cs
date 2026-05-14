using DCL.Audio;
using DCL.VoiceChat.Nearby;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.VoiceChat.Tests
{
    /// <summary>
    ///     Documents <see cref="NearbyMicrophoneAudioToggleHandler"/> as an activation-aware boolean → SFX adapter:
    ///     plays the on/off clip on every <see cref="NearbyVoiceChatStateModel.IsOpenMic"/> transition, at
    ///     <c>0.2×</c> the asset volume when the session was triggered by push-to-talk, and at full volume otherwise.
    /// </summary>
    public class NearbyMicrophoneAudioToggleHandlerShould
    {
        private const float PUSH_TO_TALK_SCALE = 0.2f;
        private const float DEFAULT_SCALE = 1f;
        private const float TOLERANCE = 0.0001f;

        private NearbyVoiceChatStateModel stateModel = null!;
        private AudioClipConfig offClip = null!;
        private AudioClipConfig onClip = null!;
        private List<(AudioClipConfig clip, float scale)> played = null!;
        private NearbyMicrophoneAudioToggleHandler handler = null!;

        [SetUp]
        public void SetUp()
        {
            stateModel = new NearbyVoiceChatStateModel(NearbyVoiceChatState.IDLE);
            offClip = ScriptableObject.CreateInstance<AudioClipConfig>();
            onClip = ScriptableObject.CreateInstance<AudioClipConfig>();
            offClip.name = "off";
            onClip.name = "on";

            played = new List<(AudioClipConfig, float)>();
            UIAudioEventsBus.Instance.PlayUIAudioEvent += OnPlay;

            handler = new NearbyMicrophoneAudioToggleHandler(stateModel, offClip, onClip);
        }

        [TearDown]
        public void TearDown()
        {
            UIAudioEventsBus.Instance.PlayUIAudioEvent -= OnPlay;
            handler.Dispose();
            stateModel.Dispose();
            Object.DestroyImmediate(offClip);
            Object.DestroyImmediate(onClip);
        }

        private void OnPlay(AudioClipConfig clip, float scale) =>
            played.Add((clip, scale));

        [Test]
        public void NotPlayAnythingAtConstruction()
        {
            Assert.That(played, Is.Empty);
        }

        [Test]
        public void PlayOnClipAtFullVolumeForButtonActivation()
        {
            stateModel.StartSpeaking(NearbyVoiceActivation.BUTTON);

            Assert.That(played.Count, Is.EqualTo(1));
            Assert.That(played[0].clip, Is.SameAs(onClip));
            Assert.That(played[0].scale, Is.EqualTo(DEFAULT_SCALE).Within(TOLERANCE));
        }

        [Test]
        public void PlayOffClipAtFullVolumeForButtonActivation()
        {
            stateModel.StartSpeaking(NearbyVoiceActivation.BUTTON);
            played.Clear();

            stateModel.StopSpeaking();

            Assert.That(played.Count, Is.EqualTo(1));
            Assert.That(played[0].clip, Is.SameAs(offClip));
            Assert.That(played[0].scale, Is.EqualTo(DEFAULT_SCALE).Within(TOLERANCE));
        }

        [Test]
        public void PlayOnClipAtReducedVolumeForPushToTalk()
        {
            stateModel.StartSpeaking(NearbyVoiceActivation.PUSH_TO_TALK);

            Assert.That(played.Count, Is.EqualTo(1));
            Assert.That(played[0].clip, Is.SameAs(onClip));
            Assert.That(played[0].scale, Is.EqualTo(PUSH_TO_TALK_SCALE).Within(TOLERANCE));
        }

        [Test]
        public void PlayOffClipAtReducedVolumeForPushToTalk()
        {
            stateModel.StartSpeaking(NearbyVoiceActivation.PUSH_TO_TALK);
            played.Clear();

            stateModel.StopSpeaking();

            Assert.That(played.Count, Is.EqualTo(1));
            Assert.That(played[0].clip, Is.SameAs(offClip));
            Assert.That(played[0].scale, Is.EqualTo(PUSH_TO_TALK_SCALE).Within(TOLERANCE));
        }

        [Test]
        public void PlayAtFullVolumeForFocusResumed()
        {
            stateModel.StartSpeaking(NearbyVoiceActivation.FOCUS_RESUMED);

            Assert.That(played.Count, Is.EqualTo(1));
            Assert.That(played[0].scale, Is.EqualTo(DEFAULT_SCALE).Within(TOLERANCE));
        }

        [Test]
        public void StopReactingAfterDispose()
        {
            handler.Dispose();

            stateModel.StartSpeaking(NearbyVoiceActivation.BUTTON);

            Assert.That(played, Is.Empty);
        }
    }
}
