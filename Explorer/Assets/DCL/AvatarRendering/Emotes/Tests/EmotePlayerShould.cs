using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Emotes.Play;
using DCL.AvatarRendering.Loading.Assets;
using ECS.StreamableLoading;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.AvatarRendering.Emotes.Tests
{
    public class EmotePlayerShould
    {
        private GameObject poolRoot = null!;
        private GameObject audioSourcePrefab = null!;
        private GameObject legacyEmoteAsset = null!;
        private GameObject avatarGameObject = null!;
        private EmotePlayer emotePlayer = null!;
        private IAvatarView avatarView = null!;

        [SetUp]
        public void Setup()
        {
            poolRoot = new GameObject("ROOT_POOL_CONTAINER");
            audioSourcePrefab = new GameObject("EmoteAudioSource");
            audioSourcePrefab.AddComponent<AudioSource>();

            emotePlayer = new EmotePlayer(audioSourcePrefab.GetComponent<AudioSource>(),
                ScriptableObject.CreateInstance<EmoteMaskCatalog>(), legacyAnimationsEnabled: false);

            // Legacy emote asset: an Animator with no controller (so CreateNewEmoteReference takes the
            // legacy branch) plus an Animation carrying a legacy clip.
            legacyEmoteAsset = new GameObject("LegacyEmoteAsset");
            legacyEmoteAsset.AddComponent<Animator>();
            var animation = legacyEmoteAsset.AddComponent<Animation>();
            var clip = new AnimationClip { legacy = true, name = "wave" };
            animation.AddClip(clip, clip.name);

            avatarGameObject = new GameObject("Avatar");

            avatarView = Substitute.For<IAvatarView>();
            avatarView.GetTransform().Returns(avatarGameObject.transform);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(avatarGameObject);
            Object.DestroyImmediate(legacyEmoteAsset);
            Object.DestroyImmediate(audioSourcePrefab);
            Object.DestroyImmediate(poolRoot);
        }

        [Test]
        public void NotLeakPooledInstanceWhenLegacyEmoteRejected()
        {
            var emoteComponent = new CharacterEmoteComponent();
            AttachmentRegularAsset sourceAsset = NewSourceAsset(legacyEmoteAsset);

            bool played = emotePlayer.Play(sourceAsset, null, false, false, in avatarView, ref emoteComponent);

            Assert.IsFalse(played);
            Assert.IsNull(emoteComponent.CurrentEmoteReference);
            Assert.IsNull(avatarGameObject.GetComponentInChildren<EmoteReferences>(true),
                "Rejected legacy emote leaked a pooled instance under the avatar.");
            Assert.AreEqual(0, sourceAsset.ReferenceCount,
                "The rejected play must return the reference it took on the source asset.");
        }

        [Test]
        public void PinSourceAssetWhilePlayingAndUnpinOnStop()
        {
            GameObject mecanimAsset = NewMecanimEmoteAsset("MecanimEmoteAsset");
            AttachmentRegularAsset sourceAsset = NewSourceAsset(mecanimAsset);
            var emoteComponent = new CharacterEmoteComponent();

            Assert.IsTrue(emotePlayer.Play(sourceAsset, null, false, false, in avatarView, ref emoteComponent));
            Assert.AreEqual(1, sourceAsset.ReferenceCount,
                "A playing emote must keep its source asset referenced, or the storage could dispose it mid-play.");

            emotePlayer.Stop(emoteComponent.CurrentEmoteReference!);
            emoteComponent.Reset();

            Assert.AreEqual(0, sourceAsset.ReferenceCount,
                "Releasing the instance back to the pool must return the play-time reference.");

            Object.DestroyImmediate(mecanimAsset);
        }

        [Test]
        public void PruneStalePoolWhenSourceAssetDestroyed()
        {
            GameObject assetA = NewMecanimEmoteAsset("EmoteAssetA");
            AttachmentRegularAsset sourceA = NewSourceAsset(assetA);
            var emoteComponent = new CharacterEmoteComponent();

            Assert.IsTrue(emotePlayer.Play(sourceA, null, false, false, in avatarView, ref emoteComponent));
            emotePlayer.Stop(emoteComponent.CurrentEmoteReference!);
            emoteComponent.Reset();

            Assert.AreEqual(1, poolRoot.GetComponentsInChildren<EmoteReferences>(true).Length,
                "The released instance should be parked under the pool root.");

            // Simulates the storage unloading the emote: its main asset is gone, so the pool keyed
            // by it can never serve an instance again.
            Object.DestroyImmediate(assetA);

            GameObject assetB = NewMecanimEmoteAsset("EmoteAssetB");
            AttachmentRegularAsset sourceB = NewSourceAsset(assetB);

            Assert.IsTrue(emotePlayer.Play(sourceB, null, false, false, in avatarView, ref emoteComponent));

            Assert.AreEqual(0, poolRoot.GetComponentsInChildren<EmoteReferences>(true).Length,
                "Instances pooled for a destroyed source asset must be destroyed with their pool.");

            emotePlayer.Stop(emoteComponent.CurrentEmoteReference!);
            Object.DestroyImmediate(assetB);
        }

        private static GameObject NewMecanimEmoteAsset(string name)
        {
            // An Animator without controller and no Animation component resolves to a non-legacy
            // EmoteReferences with no clips, which the mecanim path plays against the mocked view.
            var asset = new GameObject(name);
            asset.AddComponent<Animator>();
            return asset;
        }

        private static AttachmentRegularAsset NewSourceAsset(GameObject mainAsset) =>
            new (mainAsset, new List<AttachmentRegularAsset.RendererInfo>(), new NoopRefCountData());

        private class NoopRefCountData : IStreamableRefCountData
        {
            public void Dispose() { }

            public void Dereference() { }
        }
    }
}
