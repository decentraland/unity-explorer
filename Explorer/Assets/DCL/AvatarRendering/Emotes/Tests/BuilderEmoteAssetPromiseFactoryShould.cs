using Arch.Core;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.DTO;
using DCL.Diagnostics;
using DCL.Ipfs;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using NUnit.Framework;
using System;
using AudioPromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AudioClips.AudioClipData, ECS.StreamableLoading.AudioClips.GetAudioClipIntention>;
using GltfPromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.GLTF.GLTFData, ECS.StreamableLoading.GLTF.GetGLTFIntention>;
using MockEmote = DCL.AvatarRendering.Emotes.Tests.FinalizeEmoteLoadingSystemShould.MockEmote;
using MockEmoteStorage = DCL.AvatarRendering.Emotes.Tests.FinalizeEmoteLoadingSystemShould.MockEmoteStorage;

namespace DCL.AvatarRendering.Emotes.Tests
{
    public class BuilderEmoteAssetPromiseFactoryShould
    {
        private const string CONTENT_URL = "https://peer.decentraland.org/content/contents/";
        private static readonly URN EMOTE_URN = new ("urn:test:builder-emote");

        private World world = null!;
        private IURLBuilder urlBuilder = null!;
        private MockEmoteStorage storage = null!;

        [SetUp]
        public void SetUp()
        {
            world = World.Create();
            urlBuilder = new URLBuilder();
            storage = new MockEmoteStorage();
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();
        }

        [Test]
        public void ReturnFalse_WhenContentDownloadUrlIsEmpty()
        {
            MockEmote emote = CreateEmote(contentDownloadUrl: string.Empty, unisex: true, content: new[] { ("clip.glb", "hash1") });

            bool result = BuilderEmoteAssetPromiseFactory.TryCreate(world, emote, PartitionComponent.TOP_PRIORITY, storage, urlBuilder);

            Assert.IsFalse(result, "Without a download URL the factory cannot resolve content paths, so it must bail out.");
            Assert.AreEqual(0, CountComponents<GltfPromise>());
        }

        [Test]
        public void ReturnTrue_WhenEmoteIsAlreadyLoading()
        {
            MockEmote emote = CreateEmote(contentDownloadUrl: CONTENT_URL, unisex: true, content: new[] { ("clip.glb", "hash1") });
            emote.IsLoading = true;

            bool result = BuilderEmoteAssetPromiseFactory.TryCreate(world, emote, PartitionComponent.TOP_PRIORITY, storage, urlBuilder);

            Assert.IsTrue(result, "IsLoading short-circuit must still signal that the emote is being processed.");
            Assert.AreEqual(0, CountComponents<GltfPromise>(),
                "An already-loading emote must not spawn duplicate promises — the in-flight flow owns completion.");
        }

        [Test]
        public void ReturnFalse_WhenUnisexSharesClipAndEitherSlotAlreadyLoaded()
        {
            MockEmote emote = CreateEmote(contentDownloadUrl: CONTENT_URL, unisex: true, content: new[] { ("clip.glb", "hash1") });
            emote.MockHasSameClipForAllGendersValue = true;
            emote.AssetResults[BodyShape.MALE] = DummyAssetResult();
            storage.Set(emote.Urn, emote);

            bool result = BuilderEmoteAssetPromiseFactory.TryCreate(world, emote, PartitionComponent.TOP_PRIORITY, storage, urlBuilder);

            Assert.IsFalse(result);
            Assert.AreEqual(0, CountComponents<GltfPromise>(),
                "Unisex-shared-clip emotes are considered complete once either gender slot is populated.");
        }

        [Test]
        public void ReturnFalse_WhenNonUnisexAndBothSlotsAlreadyLoaded()
        {
            MockEmote emote = CreateEmote(contentDownloadUrl: CONTENT_URL, unisex: false, content: new[] { ("clip.glb", "hash1") });
            emote.AssetResults[BodyShape.MALE] = DummyAssetResult();
            emote.AssetResults[BodyShape.FEMALE] = DummyAssetResult();
            storage.Set(emote.Urn, emote);

            bool result = BuilderEmoteAssetPromiseFactory.TryCreate(world, emote, PartitionComponent.TOP_PRIORITY, storage, urlBuilder);

            Assert.IsFalse(result);
            Assert.AreEqual(0, CountComponents<GltfPromise>());
        }

        [Test]
        public void CreateGltfPromiseForBothBodyShapes_WhenEmoteIsUnisex()
        {
            MockEmote emote = CreateEmote(contentDownloadUrl: CONTENT_URL, unisex: true, content: new[] { ("clip.glb", "hash1") });

            bool result = BuilderEmoteAssetPromiseFactory.TryCreate(world, emote, PartitionComponent.TOP_PRIORITY, storage, urlBuilder);

            Assert.IsTrue(result, "Creating new GLB promises must report still-processing so callers keep waiting.");
            Assert.AreEqual(2, CountComponents<GltfPromise>(),
                "Unisex .glb must create one GltfPromise per body shape so both avatar genders can animate.");
            Assert.IsTrue(emote.IsLoading,
                "UpdateLoadingStatus(true) must be called so follow-up TryCreate calls short-circuit while the load is in flight.");
        }

        [Test]
        public void CreateGltfPromiseOnlyForRepresentationBodyShape_WhenEmoteIsGendered()
        {
            MockEmote emote = CreateEmote(
                contentDownloadUrl: CONTENT_URL,
                unisex: false,
                bodyShape: BodyShape.FEMALE.Value,
                content: new[] { ("clip.glb", "hash1") });

            bool result = BuilderEmoteAssetPromiseFactory.TryCreate(world, emote, PartitionComponent.TOP_PRIORITY, storage, urlBuilder);

            Assert.IsTrue(result);
            Assert.AreEqual(1, CountComponents<GltfPromise>(),
                "Gendered emotes must only spawn a promise for the body shape declared in their representation.");
            Assert.IsNull(emote.AssetResults[BodyShape.MALE],
                "The non-target body shape slot must remain untouched.");
        }

        [Test]
        public void SkipBodyShape_WhenAssetResultAlreadyPopulated()
        {
            MockEmote emote = CreateEmote(contentDownloadUrl: CONTENT_URL, unisex: true, content: new[] { ("clip.glb", "hash1") });
            emote.AssetResults[BodyShape.MALE] = DummyAssetResult();

            bool result = BuilderEmoteAssetPromiseFactory.TryCreate(world, emote, PartitionComponent.TOP_PRIORITY, storage, urlBuilder);

            Assert.IsTrue(result);
            Assert.AreEqual(1, CountComponents<GltfPromise>(),
                "Already-populated slots must be skipped so the existing asset isn't clobbered by a duplicate load.");
        }

        [Test]
        public void CreateAudioPromiseForEachBodyShape_WhenContentHasAudioFile()
        {
            MockEmote emote = CreateEmote(
                contentDownloadUrl: CONTENT_URL,
                unisex: true,
                content: new[] { ("sound.mp3", "audiohash") });

            BuilderEmoteAssetPromiseFactory.TryCreate(world, emote, PartitionComponent.TOP_PRIORITY, storage, urlBuilder);

            Assert.AreEqual(2, CountComponents<AudioPromise>(),
                "Unisex .mp3 content must produce one audio promise per body shape so both avatar genders play the emote SFX.");
        }

        private int CountComponents<T>() where T: struct
        {
            var query = new QueryDescription().WithAll<T>();
            var count = 0;
            world.Query(query, (ref T _) => count++);
            return count;
        }

        private static StreamableLoadingResult<Loading.Assets.AttachmentRegularAsset>? DummyAssetResult() =>
            new StreamableLoadingResult<Loading.Assets.AttachmentRegularAsset>(ReportData.UNSPECIFIED, new Exception("dummy"));

        private MockEmote CreateEmote(string contentDownloadUrl, bool unisex, (string file, string hash)[] content, string? bodyShape = null)
        {
            var dto = new TestEmoteDTO();
            dto.SetContentDownloadUrl(contentDownloadUrl);
            dto.id = EMOTE_URN.ToString();

            AvatarAttachmentDTO.Representation[] representations = unisex
                ? new[]
                {
                    new AvatarAttachmentDTO.Representation { bodyShapes = new[] { BodyShape.MALE.Value }, contents = Array.Empty<string>() },
                    new AvatarAttachmentDTO.Representation { bodyShapes = new[] { BodyShape.FEMALE.Value }, contents = Array.Empty<string>() },
                }
                : new[]
                {
                    new AvatarAttachmentDTO.Representation { bodyShapes = new[] { bodyShape ?? BodyShape.MALE.Value }, contents = Array.Empty<string>() },
                };

            dto.metadata = new EmoteDTO.EmoteMetadataDto
            {
                id = EMOTE_URN.ToString(),
                name = "Test",
                emoteDataADR74 = new EmoteDTO.EmoteMetadataDto.Data
                {
                    representations = representations,
                },
            };

            dto.content = BuildContent(content);

            var emote = new MockEmote(EMOTE_URN, storage);
            emote.ApplyAndMarkAsLoaded(dto);
            return emote;
        }

        private static ContentDefinition[] BuildContent((string file, string hash)[] pairs)
        {
            var arr = new ContentDefinition[pairs.Length];
            for (var i = 0; i < pairs.Length; i++)
                arr[i] = new ContentDefinition { file = pairs[i].file, hash = pairs[i].hash };
            return arr;
        }

        private class TestEmoteDTO : EmoteDTO
        {
            public void SetContentDownloadUrl(string? url) =>
                ContentDownloadUrl = url;
        }
    }
}
