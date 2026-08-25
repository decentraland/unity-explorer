using Arch.Core;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Emotes.Play;
using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Loading.Components;
using DCL.ECSComponents;
using DCL.Multiplayer.Emotes;
using DCL.Utilities;
using ECS.StreamableLoading.Common.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.AvatarRendering.Emotes.Tests
{
    public class SceneMaskedEmoteSystemShould : UnitySystemTestBase<SceneMaskedEmoteSystem>
    {
        private const string EMOTE_URN_FORMAT = "urn:decentraland:off-chain:scene-emote:test-scene-bafkreiemotehash-{0}";

        private World globalWorld = null!;
        private Entity globalPlayerEntity;
        private IEmoteStorage emoteStorage = null!;
        private EmoteMaskCatalog emoteMaskCatalog = null!;
        private GameObject poolRoot = null!;
        private GameObject audioSourcePrefab = null!;
        private GameObject avatarBaseGameObject = null!;

        [SetUp]
        public void SetUp()
        {
            // EmotePlayer resolves its pool parent with GameObject.Find("ROOT_POOL_CONTAINER") and
            // throws without it, so the object has to exist before the constructor runs.
            poolRoot = new GameObject("ROOT_POOL_CONTAINER");

            audioSourcePrefab = new GameObject("EmoteAudioSource");
            AudioSource audioSource = audioSourcePrefab.AddComponent<AudioSource>();
            emoteMaskCatalog = ScriptableObject.CreateInstance<EmoteMaskCatalog>();
            var emotePlayer = new EmotePlayer(audioSource, emoteMaskCatalog, legacyAnimationsEnabled: true);

            globalWorld = World.Create();
            globalPlayerEntity = globalWorld.Create(new AvatarShapeComponent { BodyShape = BodyShape.MALE });

            avatarBaseGameObject = new GameObject(nameof(AvatarBase));
            var avatarBaseProxy = new ObjectProxy<AvatarBase>();
            avatarBaseProxy.SetObject(avatarBaseGameObject.AddComponent<AvatarBase>());

            emoteStorage = Substitute.For<IEmoteStorage>();

            // The player stands in the scene that triggered the emote, so the play conditions are met.
            ISceneStateProvider sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneStateProvider.IsCurrent.Returns(true);

            system = new SceneMaskedEmoteSystem(world, globalWorld, globalPlayerEntity, avatarBaseProxy,
                emotePlayer, emoteStorage, Substitute.For<IEmotesMessageBus>(), sceneStateProvider);
        }

        protected override void OnTearDown()
        {
            globalWorld.Dispose();
            Object.DestroyImmediate(emoteMaskCatalog);
            Object.DestroyImmediate(avatarBaseGameObject);
            Object.DestroyImmediate(audioSourcePrefab);
            Object.DestroyImmediate(poolRoot);
        }

        [Test]
        public void DiscardNonLoopingEmoteThatAlreadyPlayed()
        {
            Entity entity = CreateSuspendedMaskedEmote(loop: false);

            system!.Update(0);

            CharacterMaskedEmoteComponent masked = world.Get<CharacterMaskedEmoteComponent>(entity);

            Assert.IsTrue(masked.EmoteUrn.IsNullOrEmpty(),
                "A one-shot masked emote that already played must not stay resumable: while its urn is set, every frame that meets the play conditions starts the emote again.");
            Assert.IsNull(masked.CurrentEmoteReference);
            Assert.IsFalse(globalWorld.Has<EmotePendingToBroadcast>(globalPlayerEntity),
                "Nothing was played, so nothing must be broadcast.");
        }

        [Test]
        public void KeepLoopingEmoteResumable()
        {
            Entity entity = CreateSuspendedMaskedEmote(loop: true);

            system!.Update(0);

            CharacterMaskedEmoteComponent masked = world.Get<CharacterMaskedEmoteComponent>(entity);

            Assert.IsFalse(masked.EmoteUrn.IsNullOrEmpty(),
                "A looping masked emote is resumed once the play conditions are met again, so its urn must survive being suspended.");
        }

        /// <summary>
        /// A masked emote in the state a non-permanent stop leaves behind: the animation is torn down
        /// but the urn is kept so that the emote can be resumed.
        /// </summary>
        private Entity CreateSuspendedMaskedEmote(bool loop)
        {
            IEmote emote = Substitute.For<IEmote>();
            emote.IsLoading.Returns(false);
            emote.IsLooping().Returns(loop);

            // Empty results: the asset is not resident, so playback stops short of touching the avatar view.
            emote.AssetResults.Returns(new StreamableLoadingResult<AttachmentRegularAsset>?[BodyShape.COUNT]);

            emoteStorage.TryGetElement(Arg.Any<URN>(), out Arg.Any<IEmote>())
                        .Returns(call =>
                         {
                             call[1] = emote;
                             return true;
                         });

            return world.Create(new CharacterMaskedEmoteComponent
            {
                EmoteUrn = string.Format(EMOTE_URN_FORMAT, loop.ToString().ToLower()),
                Mask = AvatarEmoteMask.AemUpperBody,
            });
        }
    }
}
