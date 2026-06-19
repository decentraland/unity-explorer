using DCL.Audio;
using DCL.Utilities;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.VoiceChat.Tests
{
    /// <summary>
    /// Documents <see cref="MicrophoneAudioToggleHandler"/> as a generic boolean → SFX adapter,
    /// reused by Community and Nearby voice chats.
    /// </summary>
    public class MicrophoneAudioToggleHandlerShould
    {
        private ReactiveProperty<bool> source = null!;
        private AudioClipConfig offClip = null!;
        private AudioClipConfig onClip = null!;
        private List<AudioClipConfig> played = null!;
        private MicrophoneAudioToggleHandler handler = null!;

        [SetUp]
        public void SetUp()
        {
            source = new ReactiveProperty<bool>(false);
            offClip = ScriptableObject.CreateInstance<AudioClipConfig>();
            onClip = ScriptableObject.CreateInstance<AudioClipConfig>();
            offClip.name = "off";
            onClip.name = "on";

            played = new List<AudioClipConfig>();
            UIAudioEventsBus.Instance.PlayUIAudioEvent += OnPlay;

            handler = new MicrophoneAudioToggleHandler(source, offClip, onClip);
        }

        [TearDown]
        public void TearDown()
        {
            UIAudioEventsBus.Instance.PlayUIAudioEvent -= OnPlay;
            handler.Dispose();
            Object.DestroyImmediate(offClip);
            Object.DestroyImmediate(onClip);
        }

        private void OnPlay(AudioClipConfig clip, float volumeScale) =>
            played.Add(clip);

        [Test]
        public void NotPlayAnythingAtConstruction()
        {
            Assert.That(played, Is.Empty);
        }

        [Test]
        public void PlayOnClipWhenSourceFlipsTrue()
        {
            // Act
            source.Value = true;

            // Assert
            Assert.That(played, Is.EqualTo(new[] { onClip }));
        }

        [Test]
        public void PlayOffClipWhenSourceFlipsFalse()
        {
            // Arrange
            source.Value = true;
            played.Clear();

            // Act
            source.Value = false;

            // Assert
            Assert.That(played, Is.EqualTo(new[] { offClip }));
        }

        [Test]
        public void IgnoreNoOpAssignments()
        {
            // Act — assigning the same value should not trigger ReactiveProperty.OnUpdate
            source.Value = false;
            source.Value = false;

            // Assert
            Assert.That(played, Is.Empty);
        }

        [Test]
        public void EmitOneClipPerTransitionDirection()
        {
            // Act — full toggle cycle
            source.Value = true;
            source.Value = false;
            source.Value = true;

            // Assert
            Assert.That(played, Is.EqualTo(new[] { onClip, offClip, onClip }));
        }

        [Test]
        public void StopReactingAfterDispose()
        {
            // Arrange
            handler.Dispose();

            // Act
            source.Value = true;

            // Assert
            Assert.That(played, Is.Empty);
        }
    }
}
