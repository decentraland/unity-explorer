using System.Collections.Generic;
using System.Text.RegularExpressions;
using Data;
using Loading;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests
{
    public class EmoteAnimationControllerShould
    {
        private const float TOLERANCE = 0.0001f;
        private const float CLIP_LENGTH = 2f;

        // The state name of the fixture's emote, its embedded URN.
        private const string EMOTE_URN = "embedded:test";

        private GameObject _rig;
        private AnimationClip _idleClip;
        private AnimationClip _emoteClip;
        private EmoteAnimationController _controller;
        private List<(EmotePlaybackEvent playbackEvent, float time)> _reported;

        [SetUp]
        public void SetUp()
        {
            _rig = new GameObject("rig");
            var animation = _rig.AddComponent<Animation>();
            var audioSource = _rig.AddComponent<AudioSource>();

            _idleClip = LegacyClip(EmoteAnimationController.IDLE_CLIP_NAME, WrapMode.Loop);
            animation.AddClip(_idleClip, EmoteAnimationController.IDLE_CLIP_NAME);
            _emoteClip = LegacyClip("emote", WrapMode.Clamp);

            _controller = _rig.AddComponent<EmoteAnimationController>();

            // The references are serialized fields the scene wires; the fixture wires them the same way.
            var serialized = new SerializedObject(_controller);
            serialized.FindProperty("avatarAnimation").objectReferenceValue = animation;
            serialized.FindProperty("audioSource").objectReferenceValue = audioSource;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            _reported = new List<(EmotePlaybackEvent, float)>();
            _controller.PlaybackChanged += (playbackEvent, time) => _reported.Add((playbackEvent, time));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_rig);
            Object.DestroyImmediate(_idleClip);
            Object.DestroyImmediate(_emoteClip);
        }

        [TestCase(0f, 2f, 0, 0f)]
        [TestCase(1.5f, 2f, 0, 1.5f)]
        [TestCase(2f, 2f, 1, 0f)]
        [TestCase(7f, 3f, 2, 1f)]
        public void SplitAClipTimeIntoWrapsAndPosition(float time, float length, int loops, float clipTime)
        {
            // Act
            var wrapped = EmoteAnimationController.WrapClipTime(time, length);

            // Assert
            Assert.AreEqual(loops, wrapped.loops);
            Assert.AreEqual(clipTime, wrapped.clipTime, TOLERANCE);
        }

        [Test]
        public void ReportNothingForAnEmptyClip()
        {
            // Act
            var wrapped = EmoteAnimationController.WrapClipTime(3f, 0f);

            // Assert
            Assert.AreEqual(0, wrapped.loops);
            Assert.AreEqual(0f, wrapped.clipTime, TOLERANCE);
        }

        [Test]
        public void ReportPlayPauseAndAFreshPlayAfterAStop()
        {
            // Act
            PlayEmote();
            _controller.PauseEmote();
            var pausedBeforeStop = _controller.IsPaused;
            _controller.StopEmote();
            var pausedAfterStop = _controller.IsPaused;
            _controller.ReplayEmote();

            // Assert
            Assert.IsTrue(pausedBeforeStop);
            Assert.IsFalse(pausedAfterStop);
            CollectionAssert.AreEqual(new[]
            {
                EmotePlaybackEvent.Play, EmotePlaybackEvent.Pause, EmotePlaybackEvent.End, EmotePlaybackEvent.Play,
            }, Events());
            Assert.AreEqual(0f, _reported[2].time, TOLERANCE);
        }

        [Test]
        public void ReportTheEndOnceWhenAFinishedClipReturnsToIdle()
        {
            // Arrange
            PlayEmote();

            // Act: the clip's own end event, a frame of the fade that follows, then the reload's cleanup.
            Invoke("EmoteEnded");
            Tick();
            _controller.ReturnToIdle(false);

            // Assert
            CollectionAssert.AreEqual(new[] { EmotePlaybackEvent.Play, EmotePlaybackEvent.End }, Events());
            Assert.AreEqual(CLIP_LENGTH, _reported[1].time, TOLERANCE);
        }

        [Test]
        public void ReportTheEndOnceWhenAStoppedClipReturnsToIdle()
        {
            // Arrange
            PlayEmote();

            // Act
            _controller.StopEmote();
            _controller.ReturnToIdle(false);

            // Assert
            CollectionAssert.AreEqual(new[] { EmotePlaybackEvent.Play, EmotePlaybackEvent.End }, Events());
        }

        [Test]
        public void ReportNoPositionWhileHeld()
        {
            // Arrange
            PlayEmote();
            _controller.StopEmote();

            // Act
            _controller.GoToEmote(0.5f);
            Tick();

            // Assert
            CollectionAssert.DoesNotContain(Events(), EmotePlaybackEvent.Playing);
        }

        [Test]
        public void ReportThePositionOfASeekWhilePlaying()
        {
            // Arrange
            PlayEmote();

            // Act
            _controller.GoToEmote(0.5f);

            // Assert
            Assert.AreEqual(EmotePlaybackEvent.Playing, _reported[^1].playbackEvent);
            Assert.AreEqual(0.5f, _reported[^1].time, TOLERANCE);
        }

        [Test]
        public void ClampASeekToTheClip()
        {
            // Arrange
            PlayEmote();

            // Act
            _controller.GoToEmote(CLIP_LENGTH * 2f);
            _controller.GoToEmote(-1f);

            // Assert
            Assert.AreEqual(CLIP_LENGTH, _reported[^2].time, TOLERANCE);
            Assert.AreEqual(0f, _reported[^1].time, TOLERANCE);
        }

        [Test]
        public void ResumeAPausedClipSeekedToItsEndAtItsEnd()
        {
            // Arrange
            PlayEmote();
            _controller.PauseEmote();
            _controller.GoToEmote(CLIP_LENGTH);

            // Act
            _controller.ResumeEmote();

            // Assert
            Assert.AreEqual(EmotePlaybackEvent.Play, _reported[^1].playbackEvent);
            Assert.AreEqual(CLIP_LENGTH, _reported[^1].time, TOLERANCE);
        }

        [Test]
        public void ReportTheEndOnceWhenTheClipRanOutWithoutItsEndEvent()
        {
            // Arrange: a seek past the end event skips it, and the clip then runs out on its own.
            PlayEmote();
            _rig.GetComponent<Animation>().Stop();

            // Act
            Tick();
            Tick();

            // Assert
            CollectionAssert.AreEqual(new[] { EmotePlaybackEvent.Play, EmotePlaybackEvent.End }, Events());
        }

        [Test]
        public void HandAPlayingEmoteToARebuiltPartnerAtItsTime()
        {
            // Arrange
            PlayEmote();
            _controller.GoToEmote(1f);
            var (partner, partnerAnimation, partnerClip) = Partner();

            try
            {
                // Act
                _controller.ReplacePartner(partner, partnerAnimation);

                // Assert
                Assert.IsTrue(partner.activeSelf);
                Assert.AreEqual(1f, partnerAnimation[EMOTE_URN].time, TOLERANCE);
            }
            finally
            {
                Object.DestroyImmediate(partner);
                Object.DestroyImmediate(partnerClip);
            }
        }

        [Test]
        public void HandAPausedEmoteToARebuiltPartnerStill()
        {
            // Arrange
            PlayEmote();
            _controller.GoToEmote(1f);
            _controller.PauseEmote();
            var (partner, partnerAnimation, partnerClip) = Partner();

            try
            {
                // Act
                _controller.ReplacePartner(partner, partnerAnimation);

                // Assert
                Assert.AreEqual(1f, partnerAnimation[EMOTE_URN].time, TOLERANCE);
                Assert.AreEqual(0f, partnerAnimation[EMOTE_URN].speed, TOLERANCE);
            }
            finally
            {
                Object.DestroyImmediate(partner);
                Object.DestroyImmediate(partnerClip);
            }
        }

        [Test]
        public void HandAHeldEmoteToARebuiltPartnerOnTheFirstFrame()
        {
            // Arrange: a seek made while paused, then a stop that holds the first frame.
            PlayEmote();
            _controller.PauseEmote();
            _controller.GoToEmote(1.5f);
            _controller.StopEmote();
            var (partner, partnerAnimation, partnerClip) = Partner();

            try
            {
                // Act
                _controller.ReplacePartner(partner, partnerAnimation);

                // Assert
                Assert.AreEqual(0f, partnerAnimation[EMOTE_URN].time, TOLERANCE);
            }
            finally
            {
                Object.DestroyImmediate(partner);
                Object.DestroyImmediate(partnerClip);
            }
        }

        [Test]
        public void KeepARebuiltPartnerHiddenOnceTheEmoteLeft()
        {
            // Arrange
            PlayEmote();
            _controller.ReturnToIdle(false);
            var (partner, partnerAnimation, partnerClip) = Partner();
            var reported = _reported.Count;

            try
            {
                // Act
                _controller.ReplacePartner(partner, partnerAnimation);

                // Assert
                Assert.IsFalse(partner.activeSelf);
                Assert.AreEqual(reported, _reported.Count);
            }
            finally
            {
                Object.DestroyImmediate(partner);
                Object.DestroyImmediate(partnerClip);
            }
        }

        [Test]
        public void ShowThePropOnAHoldAfterTheClipRanOut()
        {
            // Arrange
            var prop = new GameObject("prop");
            var propAnimation = prop.AddComponent<Animation>();
            var propClip = LegacyClip("emote", WrapMode.Clamp);
            propAnimation.AddClip(propClip, "emote");

            try
            {
                PlayEmote(prop, propAnimation);
                Invoke("EmoteEnded");
                var hiddenAfterEnd = !prop.activeSelf;

                // Act
                _controller.StopEmote();

                // Assert
                Assert.IsTrue(hiddenAfterEnd);
                Assert.IsTrue(prop.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(prop);
                Object.DestroyImmediate(propClip);
            }
        }

        // A frame of the controller.
        private void Tick() => Invoke("Update");

        private void Invoke(string method) => UnityMessages.Invoke(_controller, method);

        // In the Editor the runtime clip-event API logs an error per write (the events are set all the
        // same); the player has no such log.
        private void PlayEmote(GameObject prop = null, Animation propAnimation = null)
        {
            for (var i = 0; i < 3; i++)
            {
                LogAssert.Expect(LogType.Error, new Regex("AnimationUtility"));
            }

            _controller.PlayEmote(new LoadedEmote(EntityDefinition.FromEmbeddedEmote("test", false), _emoteClip,
                null, prop, propAnimation, null, ModelMetrics.Empty));
        }

        private List<EmotePlaybackEvent> Events()
        {
            var events = new List<EmotePlaybackEvent>(_reported.Count);
            foreach (var (playbackEvent, _) in _reported) events.Add(playbackEvent);
            return events;
        }

        // A social emote's other avatar, built hidden, with its clip under the emote's state name.
        private static (GameObject partner, Animation animation, AnimationClip clip) Partner()
        {
            var partner = new GameObject("partner");
            partner.SetActive(false);

            var animation = partner.AddComponent<Animation>();
            var clip = LegacyClip(EMOTE_URN, WrapMode.Clamp);
            animation.AddClip(clip, EMOTE_URN);
            animation.clip = clip;

            return (partner, animation, clip);
        }

        private static AnimationClip LegacyClip(string name, WrapMode wrapMode)
        {
            var clip = new AnimationClip { name = name, legacy = true, wrapMode = wrapMode };
            clip.SetCurve(string.Empty, typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, CLIP_LENGTH, 1f));
            return clip;
        }
    }
}
