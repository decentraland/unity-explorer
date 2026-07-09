using NUnit.Framework;
using UnityEngine;
using Utility.Animations;

namespace DCL.AvatarRendering.Emotes.Tests
{
    public class CharacterEmoteComponentShould
    {
        private GameObject gameObject = null!;
        private EmoteReferences emoteReferences = null!;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject(nameof(CharacterEmoteComponentShould));
            emoteReferences = gameObject.AddComponent<EmoteReferences>();
        }

        [TearDown]
        public void TearDown()
        {
            if (gameObject != null) Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void IsPlayingEmote_ReturnsFalse_WhenNoReferenceAndNoAnimatorTag()
        {
            var emoteComponent = new CharacterEmoteComponent();

            Assert.IsFalse(emoteComponent.IsPlayingEmote);
        }

        [Test]
        public void IsPlayingEmote_ReturnsTrue_WhenLegacyReferenceIsSet()
        {
            emoteReferences.Initialize(null, null, null, null, 0, legacy: true);

            var emoteComponent = new CharacterEmoteComponent { CurrentEmoteReference = emoteReferences };

            Assert.IsTrue(emoteComponent.IsPlayingEmote,
                "Legacy emotes bypass the Mecanim animator so IsPlayingEmote must honour the reference's legacy flag.");
        }

        [Test]
        public void IsPlayingEmote_ReturnsFalse_WhenNonLegacyReferenceAndNoAnimatorTag()
        {
            emoteReferences.Initialize(null, null, null, null, 0, legacy: false);

            var emoteComponent = new CharacterEmoteComponent { CurrentEmoteReference = emoteReferences };

            Assert.IsFalse(emoteComponent.IsPlayingEmote,
                "Mecanim emotes only count as playing while the animator is actually in the EMOTE or EMOTE_LOOP tag.");
        }

        [Test]
        public void IsPlayingEmote_ReturnsTrue_WhenAnimatorInEmoteTag()
        {
            var emoteComponent = new CharacterEmoteComponent();
            emoteComponent.SetAnimationTag(AnimationHashes.EMOTE);

            Assert.IsTrue(emoteComponent.IsPlayingEmote);
        }

        [Test]
        public void IsPlayingEmote_ReturnsTrue_WhenAnimatorInEmoteLoopTag()
        {
            var emoteComponent = new CharacterEmoteComponent();
            emoteComponent.SetAnimationTag(AnimationHashes.EMOTE_LOOP);

            Assert.IsTrue(emoteComponent.IsPlayingEmote);
        }

        [Test]
        public void Reset_ZeroesPlayingTime()
        {
            var emoteComponent = new CharacterEmoteComponent { PlayingTime = 5f };

            emoteComponent.Reset();

            Assert.AreEqual(0f, emoteComponent.PlayingTime);
        }
    }
}
