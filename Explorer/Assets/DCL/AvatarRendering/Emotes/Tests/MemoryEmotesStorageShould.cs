using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Loading.Components;
using DCL.Optimization.PerformanceBudgeting;
using ECS.StreamableLoading;
using ECS.StreamableLoading.Common.Components;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.AvatarRendering.Emotes.Tests
{
    public class MemoryEmotesStorageShould
    {
        private MemoryEmotesStorage storage = null!;
        private GameObject mainAsset = null!;
        private CountingRefCountData refCountData = null!;
        private AttachmentRegularAsset asset = null!;

        [SetUp]
        public void SetUp()
        {
            storage = new MemoryEmotesStorage();
            mainAsset = new GameObject("EmoteMainAsset");
            refCountData = new CountingRefCountData();
            asset = new AttachmentRegularAsset(mainAsset, new List<AttachmentRegularAsset.RendererInfo>(), refCountData);
        }

        [TearDown]
        public void TearDown()
        {
            if (mainAsset != null) Object.DestroyImmediate(mainAsset);
        }

        [Test]
        public void KeepReferencedEmoteAssetsOnUnload()
        {
            var urn = new URN("urn:test:emote:referenced");
            IEmote emote = AddEmoteWithAsset(urn);

            asset.AddReference();

            storage.Unload(new NullPerformanceBudget());

            Assert.IsTrue(storage.TryGetElement(urn, out _), "A referenced emote must survive the unload pass.");
            Assert.IsNotNull(emote.AssetResults[BodyShape.MALE], "A referenced asset must not be discarded.");
            Assert.AreEqual(0, refCountData.DereferenceCount, "A referenced asset must not be disposed.");
        }

        [Test]
        public void DisposeAndEvictUnreferencedEmoteOnUnload()
        {
            var urn = new URN("urn:test:emote:unreferenced");
            IEmote emote = AddEmoteWithAsset(urn);

            // First pass disposes the unreferenced asset and clears its slot; the second pass
            // (all slots empty by then) evicts the emote from the storage.
            storage.Unload(new NullPerformanceBudget());

            Assert.AreEqual(1, refCountData.DereferenceCount, "An unreferenced asset must be disposed under memory pressure.");
            Assert.IsNull(emote.AssetResults[BodyShape.MALE], "The disposed asset slot must be cleared.");

            storage.Unload(new NullPerformanceBudget());

            Assert.IsFalse(storage.TryGetElement(urn, out _), "A fully unloaded emote must be evicted from the storage.");
        }

        private IEmote AddEmoteWithAsset(URN urn)
        {
            var dto = new EmoteDTO
            {
                id = urn.ToString(),
                metadata = new EmoteDTO.EmoteMetadataDto
                {
                    id = urn.ToString(),
                },
            };

            IEmote emote = storage.GetOrAddByDTO(dto);
            emote.AssetResults[BodyShape.MALE] = new StreamableLoadingResult<AttachmentRegularAsset>(asset);
            return emote;
        }

        private class CountingRefCountData : IStreamableRefCountData
        {
            public int DereferenceCount { get; private set; }

            public void Dispose() { }

            public void Dereference() => DereferenceCount++;
        }
    }
}
