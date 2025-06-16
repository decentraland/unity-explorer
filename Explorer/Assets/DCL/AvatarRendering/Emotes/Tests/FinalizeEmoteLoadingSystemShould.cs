using Arch.Core;
using AssetManagement;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.DTO;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using ECS.Prioritization.Components;
using ECS.StreamableLoading;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.AudioClips;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.GLTF;
using ECS.StreamableLoading.Textures;
using ECS.TestSuite;
using NUnit.Framework;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.TestTools;

// Define Promise types as aliases for clarity, similar to the system file
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>; // Corrected alias
using GltfPromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.GLTF.GLTFData, ECS.StreamableLoading.GLTF.GetGLTFIntention>;
using AssetBundleManifestPromise = ECS.StreamableLoading.Common.AssetPromise<SceneRunner.Scene.SceneAssetBundleManifest, DCL.AvatarRendering.Wearables.Components.GetWearableAssetBundleManifestIntention>;
using AudioPromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AudioClips.AudioClipData, ECS.StreamableLoading.AudioClips.GetAudioClipIntention>;
using EmotesFromRealmPromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesDTOList, DCL.AvatarRendering.Emotes.GetEmotesByPointersFromRealmIntention>;
using EmoteResolutionPromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesResolution, DCL.AvatarRendering.Emotes.GetEmotesByPointersIntention>;
using Object = UnityEngine.Object; // Corrected alias

namespace DCL.AvatarRendering.Emotes.Tests
{
    public class FinalizeEmoteLoadingSystemShould : UnitySystemTestBase<FinalizeEmoteLoadingSystem>
    {
        private MockEmoteStorage mockEmoteStorage;
        private ListObjectPool<URN> urnPool;

        // Shared mock objects for assets
        private GameObject mockGameObject;
        private AttachmentRegularAsset mockAttachmentAsset;
        private MockStreamableDataWithURN mockStreamableData;

        [SetUp]
        public void SetUp()
        {
            mockEmoteStorage = new MockEmoteStorage();
            urnPool = new ListObjectPool<URN>();
            system = new FinalizeEmoteLoadingSystem(world, mockEmoteStorage);

            // Common mock assets
            mockGameObject = new GameObject("MockAsset");
            mockStreamableData = new MockStreamableDataWithURN(new URN("urn:mock:asset"));
            mockAttachmentAsset = new AttachmentRegularAsset(mockGameObject, new List<AttachmentRegularAsset.RendererInfo>(), mockStreamableData);
        }

        [TearDown]
        public void TearDown()
        {
            urnPool.Dispose();
            world.Dispose();
            if (mockGameObject != null) Object.DestroyImmediate(mockGameObject);
        }

        [Test]
        public void FinalizeEmoteDTOLoadingCorrectly()
        {
            var emoteURN1 = new URN("urn:realm:emote1");
            var emoteURN2 = new URN("urn:realm:emote2");
            EmoteDTO dto1 = CreateEmoteDTO(emoteURN1, false, "Emote One");
            EmoteDTO dto2 = CreateEmoteDTO(emoteURN2, false, "Emote Two");

            IEmote mockEmote1 = new MockEmote(emoteURN1, mockEmoteStorage);
            mockEmoteStorage.Set(emoteURN1, mockEmote1); // Pre-populate for existing case

            var pointers = new List<URN> { emoteURN1, emoteURN2 };
            var intention = new GetEmotesByPointersFromRealmIntention(pointers, new CommonLoadingArguments(URLAddress.EMPTY));
            var repoolableList = RepoolableList<EmoteDTO>.NewList();
            repoolableList.List.Add(dto1);
            repoolableList.List.Add(dto2);
            var dtoListAsset = new EmotesDTOList(repoolableList);

            // Create promise struct; promise.Entity is the result-holder
            var promise = EmotesFromRealmPromise.Create(world, intention, PartitionComponent.TOP_PRIORITY);
            Entity resultHolderEntity = promise.Entity;

            // Create carrier entity and add the promise struct component to it
            Entity promiseCarrierEntity = world.Create(promise);

            // Add result to the result-holder entity
            world.Add(resultHolderEntity, new StreamableLoadingResult<EmotesDTOList>(dtoListAsset));

            system.Update(0);

            Assert.IsFalse(world.IsAlive(promiseCarrierEntity), "Carrier entity should be destroyed by the system.");
            Assert.IsFalse(world.IsAlive(resultHolderEntity));

            Assert.IsTrue(mockEmoteStorage.GetOrAddByDTOCalls.Contains(emoteURN1));
            Assert.IsFalse(mockEmote1.IsLoading);

            Assert.IsTrue(mockEmoteStorage.GetOrAddByDTOCalls.Contains(emoteURN2));
            var mockEmote2 = (MockEmote)mockEmoteStorage.Emotes[emoteURN2];
            Assert.AreEqual(1, mockEmote2.ApplyAndMarkAsLoadedCallCount);
            Assert.AreSame(dto2, mockEmote2.LastAppliedDTO);
            Assert.IsFalse(mockEmote2.IsLoading);
        }

        [Test]
        public void CancelEmoteDTOLoadingCorrectly()
        {
            var pointers = new List<URN> { new ("urn:realm:cancel") };
            var intention = new GetEmotesByPointersFromRealmIntention(pointers, new CommonLoadingArguments(URLAddress.EMPTY));

            // Create promise struct; promise.Entity is the result-holder
            var promise = EmotesFromRealmPromise.Create(world, intention, PartitionComponent.TOP_PRIORITY);
            Entity resultHolderEntity = promise.Entity;

            // Create carrier entity and add the promise struct component to it
            Entity promiseCarrierEntity = world.Create(promise);

            // ForgetLoading cancels the intention and destroys the result-holder entity
            promise.ForgetLoading(world);

            system.Update(0);

            // System should destroy the carrier entity because the promise was cancelled
            Assert.IsFalse(world.IsAlive(promiseCarrierEntity), "Carrier entity should be destroyed by the system upon cancellation.");

            // Result-holder was already destroyed by ForgetLoading
            Assert.IsFalse(world.IsAlive(resultHolderEntity));
        }

        [Test]
        public void FinalizeAssetBundleManifestLoadingCorrectly()
        {
            var emoteURN = new URN("urn:manifest:emote1");
            IEmote mockEmote = new MockEmote(emoteURN, mockEmoteStorage);
            Entity entity = world.Create(mockEmote); // Entity holding the IEmote component

            var manifest = new SceneAssetBundleManifest(URLDomain.EMPTY, "v1", Array.Empty<string>(), "hash", "date");
            var intention = new GetWearableAssetBundleManifestIntention { CommonArguments = new CommonLoadingArguments(URLAddress.EMPTY) };
            var promise = AssetBundleManifestPromise.Create(world, intention, PartitionComponent.TOP_PRIORITY);
            world.Add(entity, promise); // Promise is on the same entity as IEmote
            world.Add(promise.Entity, new StreamableLoadingResult<SceneAssetBundleManifest>(manifest)); // Result on promise's entity

            system.Update(0);

            Assert.IsFalse(world.IsAlive(entity)); // Check promise component removed
            Assert.IsFalse(world.IsAlive(promise.Entity));
            Assert.IsTrue(mockEmote.ManifestResult.HasValue && mockEmote.ManifestResult.Value.Succeeded);
            Assert.AreSame(manifest, mockEmote.ManifestResult.Value.Asset);
        }

        [Test]
        public void CancelAssetBundleManifestLoadingCorrectly()
        {
            var emoteURN = new URN("urn:manifest:cancel_emote");
            IEmote mockEmote = new MockEmote(emoteURN, mockEmoteStorage);
            Entity entity = world.Create(mockEmote); // Carrier entity

            var intention = new GetWearableAssetBundleManifestIntention { CommonArguments = new CommonLoadingArguments(URLAddress.EMPTY) };
            var promise = AssetBundleManifestPromise.Create(world, intention, PartitionComponent.TOP_PRIORITY); // promise.Entity is result-holder
            world.Add(entity, promise); // Add promise component to carrier

            Entity resultHolderEntity = promise.Entity; // Explicitly get for assertion
            promise.ForgetLoading(world); // This cancels the intention AND destroys promise.Entity (result-holder)

            system.Update(0);

            Assert.IsFalse(world.IsAlive(entity)); // Asserts carrier's promise component is gone (carrier destroyed)
            Assert.IsFalse(world.IsAlive(resultHolderEntity));
        }

        [Test]
        public void FinalizeGltfEmoteLoadingCorrectly()
        {
            var emoteURN = new URN("urn:gltf:emote_male");
            IEmote mockEmote = new MockEmote(emoteURN, mockEmoteStorage);
            mockEmote.ApplyAndMarkAsLoaded(CreateEmoteDTO(emoteURN, false));

            // Main components needed for the system query to run on the Entity
            BodyShape bodyShape = BodyShape.MALE;
            var intention = new GetGLTFIntention { CommonArguments = new CommonLoadingArguments(URLAddress.EMPTY) };
            Entity emoteEntity = CreateEmoteEntityWithPromise<GLTFData, GetGLTFIntention>(mockEmote, intention, bodyShape, out GltfPromise promise);

            // Mocking promise result
            var gltfData = new GLTFData(null, mockGameObject);
            var promiseResult = new StreamableLoadingResult<GLTFData>(gltfData);
            world.Add(promise.Entity, promiseResult);

            Assert.IsFalse(mockEmote.AssetResults[bodyShape].HasValue);

            system.Update(0);

            Assert.IsFalse(world.IsAlive(emoteEntity));
            Assert.IsFalse(world.IsAlive(promise.Entity));

            Assert.IsTrue(mockEmote.AssetResults[bodyShape].HasValue);
            StreamableLoadingResult<AttachmentRegularAsset> resultValue = mockEmote.AssetResults[bodyShape].Value;
            Assert.IsTrue(resultValue.Succeeded);

            AttachmentRegularAsset? resultingAttachment = resultValue.Asset;
            Assert.IsNotNull(resultingAttachment);
            Assert.AreSame(mockGameObject, resultingAttachment!.MainAsset);
            Assert.AreSame(gltfData, resultingAttachment.assetData);
        }

        [Test]
        public void FinalizeGltfEmoteLoadingUnisexCorrectly()
        {
            var emoteURN = new URN("urn:gltf:emote_unisex");
            IEmote mockEmote = new MockEmote(emoteURN, mockEmoteStorage) { MockIsUnisexValue = true, MockHasSameClipForAllGendersValue = true };
            mockEmote.ApplyAndMarkAsLoaded(CreateEmoteDTO(emoteURN, true)); // Mark as unisex for DTO properties

            BodyShape loadingBodyShape = BodyShape.MALE; // System will apply to both if unisex
            var intention = new GetGLTFIntention { CommonArguments = new CommonLoadingArguments(URLAddress.EMPTY) };
            Entity emoteEntity = CreateEmoteEntityWithPromise<GLTFData, GetGLTFIntention>(mockEmote, intention, loadingBodyShape, out GltfPromise promise);
            Entity resultHolderEntity = promise.Entity;

            var gltfData = new GLTFData(null, mockGameObject);
            world.Add(resultHolderEntity, new StreamableLoadingResult<GLTFData>(gltfData));

            system.Update(0);

            Assert.IsFalse(world.IsAlive(emoteEntity), "Carrier entity should be destroyed.");
            Assert.IsFalse(world.IsAlive(resultHolderEntity), "Result-holder entity should be destroyed.");

            // Check assets for both body shapes
            Assert.IsTrue(mockEmote.AssetResults[BodyShape.MALE].HasValue, "Male asset should be set for unisex.");
            Assert.IsTrue(mockEmote.AssetResults[BodyShape.MALE].Value.Succeeded, "Male asset should succeed.");
            Assert.AreSame(mockGameObject, mockEmote.AssetResults[BodyShape.MALE].Value.Asset.MainAsset, "Male asset game object should match.");

            Assert.IsTrue(mockEmote.AssetResults[BodyShape.FEMALE].HasValue, "Female asset should be set for unisex.");
            Assert.IsTrue(mockEmote.AssetResults[BodyShape.FEMALE].Value.Succeeded, "Female asset should succeed.");
            Assert.AreSame(mockGameObject, mockEmote.AssetResults[BodyShape.FEMALE].Value.Asset.MainAsset, "Female asset game object should match.");
            Assert.AreSame(mockEmote.AssetResults[BodyShape.MALE].Value.Asset, mockEmote.AssetResults[BodyShape.FEMALE].Value.Asset, "Male and Female assets should be the same instance for unisex.");

            Assert.IsFalse(mockEmote.IsLoading, "Emote should not be loading after successful unisex load.");
        }

        [Test]
        public void FinalizeGltfEmoteLoadingFailsCorrectly()
        {
            var emoteURN = new URN("urn:gltf:emote_male_fail");
            IEmote mockEmote = new MockEmote(emoteURN, mockEmoteStorage) { IsLoading = true };
            mockEmote.ApplyAndMarkAsLoaded(CreateEmoteDTO(emoteURN, false)); // DTO loaded, asset will fail
            ((MockEmote)mockEmote).IsLoading = true; // Manually set back to loading for asset part

            BodyShape bodyShape = BodyShape.MALE;
            var intention = new GetGLTFIntention { CommonArguments = new CommonLoadingArguments(URLAddress.EMPTY) };
            var exception = new Exception("Simulated GLTF load failure");

            Entity emoteEntity = CreateEmoteEntityWithPromise<GLTFData, GetGLTFIntention>(mockEmote, intention, bodyShape, out GltfPromise promise);
            Entity resultHolderEntity = promise.Entity;
            LogAssert.Expect(LogType.Exception, $"Exception: {exception.Message}");
            world.Add(resultHolderEntity, new StreamableLoadingResult<GLTFData>(ReportData.UNSPECIFIED, exception));

            system.Update(0);

            Assert.IsFalse(world.IsAlive(emoteEntity), "Carrier entity should be destroyed.");
            Assert.IsFalse(world.IsAlive(resultHolderEntity), "Result-holder entity should be destroyed by AssetPromise framework (even on failure).");

            Assert.IsNull(mockEmote.AssetResults[bodyShape], "Asset result for body shape should be null on failure.");
            Assert.IsFalse(mockEmote.IsLoading, "Emote should not be loading after a failed asset load attempt (status updated).");
        }

        [Test]
        public void FinalizeGltfEmoteLoadingCancelledCorrectly()
        {
            var emoteURN = new URN("urn:gltf:emote_male_cancel");
            IEmote mockEmote = new MockEmote(emoteURN, mockEmoteStorage);
            mockEmote.ApplyAndMarkAsLoaded(CreateEmoteDTO(emoteURN, false));
            ((MockEmote)mockEmote).IsLoading = true; // Manually set back to loading for asset part

            BodyShape bodyShape = BodyShape.MALE;
            var intention = new GetGLTFIntention { CommonArguments = new CommonLoadingArguments(URLAddress.EMPTY) };

            Entity emoteEntity = CreateEmoteEntityWithPromise<GLTFData, GetGLTFIntention>(mockEmote, intention, bodyShape, out GltfPromise promise);
            Entity resultHolderEntity = promise.Entity;

            promise.ForgetLoading(world); // This also destroys resultHolderEntity

            system.Update(0);

            Assert.IsFalse(world.IsAlive(emoteEntity), "Carrier entity should be destroyed on cancellation.");
            Assert.IsFalse(world.IsAlive(resultHolderEntity), "Result-holder entity should have been destroyed by ForgetLoading.");

            Assert.IsNull(mockEmote.AssetResults[bodyShape], "Asset result should be null after cancellation.");
            Assert.IsFalse(mockEmote.IsLoading, "Emote should not be loading after cancellation (status updated).");
        }

        [Test]
        public void FinalizeAssetBundleEmoteLoadingCorrectly()
        {
            var emoteURN = new URN("urn:ab:emote_female");
            var isUnisex = false;

            IEmote mockEmote = new MockEmote(emoteURN, mockEmoteStorage) { MockIsUnisexValue = isUnisex };
            mockEmote.ApplyAndMarkAsLoaded(CreateEmoteDTO(emoteURN, isUnisex));

            // Main components needed for the system query to run on the Entity
            BodyShape bodyShape = BodyShape.FEMALE;
            var intention = new GetAssetBundleIntention { CommonArguments = new CommonLoadingArguments(URLAddress.EMPTY) };
            Entity emoteEntity = CreateEmoteEntityWithPromise<AssetBundleData, GetAssetBundleIntention>(mockEmote, intention, bodyShape, out AssetBundlePromise promise);

            // Mocking promise result
            var assetBundleData = new AssetBundleData(null, null, mockGameObject, null);
            var promiseResult = new StreamableLoadingResult<AssetBundleData>(assetBundleData);
            world.Add(promise.Entity, promiseResult);

            Assert.IsFalse(mockEmote.AssetResults[bodyShape].HasValue);

            system.Update(0);

            Assert.IsFalse(world.IsAlive(emoteEntity));
            Assert.IsFalse(world.IsAlive(promise.Entity));

            Assert.IsTrue(mockEmote.AssetResults[bodyShape].HasValue);
            StreamableLoadingResult<AttachmentRegularAsset> resultValue = mockEmote.AssetResults[bodyShape].Value;
            Assert.IsTrue(resultValue.Succeeded);

            AttachmentRegularAsset? resultingAttachment = resultValue.Asset;
            Assert.IsNotNull(resultingAttachment);
            Assert.AreSame(mockGameObject, resultingAttachment!.MainAsset);
            Assert.AreSame(assetBundleData, resultingAttachment.assetData);
        }

        [Test]
        public void FinalizeAssetBundleEmoteLoadingUnisexCorrectly()
        {
            var emoteURN = new URN("urn:ab:emote_unisex");
            IEmote mockEmote = new MockEmote(emoteURN, mockEmoteStorage) { MockIsUnisexValue = true, MockHasSameClipForAllGendersValue = true };
            mockEmote.ApplyAndMarkAsLoaded(CreateEmoteDTO(emoteURN, true));

            BodyShape loadingBodyShape = BodyShape.FEMALE; // System will apply to both if unisex
            var intention = new GetAssetBundleIntention { CommonArguments = new CommonLoadingArguments(URLAddress.EMPTY) };
            Entity emoteEntity = CreateEmoteEntityWithPromise<AssetBundleData, GetAssetBundleIntention>(mockEmote, intention, loadingBodyShape, out AssetBundlePromise promise);
            Entity resultHolderEntity = promise.Entity;

            var assetBundleData = new AssetBundleData(null, null, mockGameObject, null);
            world.Add(resultHolderEntity, new StreamableLoadingResult<AssetBundleData>(assetBundleData));

            system.Update(0);

            Assert.IsFalse(world.IsAlive(emoteEntity), "Carrier entity should be destroyed.");
            Assert.IsFalse(world.IsAlive(resultHolderEntity), "Result-holder entity should be destroyed.");

            Assert.IsTrue(mockEmote.AssetResults[BodyShape.MALE].HasValue, "Male asset should be set for unisex.");
            Assert.IsTrue(mockEmote.AssetResults[BodyShape.MALE].Value.Succeeded, "Male asset should succeed.");
            Assert.AreSame(mockGameObject, mockEmote.AssetResults[BodyShape.MALE].Value.Asset.MainAsset, "Male asset game object should match.");

            Assert.IsTrue(mockEmote.AssetResults[BodyShape.FEMALE].HasValue, "Female asset should be set for unisex.");
            Assert.IsTrue(mockEmote.AssetResults[BodyShape.FEMALE].Value.Succeeded, "Female asset should succeed.");
            Assert.AreSame(mockGameObject, mockEmote.AssetResults[BodyShape.FEMALE].Value.Asset.MainAsset, "Female asset game object should match.");
            Assert.AreSame(mockEmote.AssetResults[BodyShape.MALE].Value.Asset, mockEmote.AssetResults[BodyShape.FEMALE].Value.Asset, "Male and Female assets should be the same instance for unisex.");

            Assert.IsFalse(mockEmote.IsLoading, "Emote should not be loading after successful unisex load.");
        }

        [Test]
        public void FinalizeAssetBundleEmoteLoadingFailsCorrectly()
        {
            var emoteURN = new URN("urn:ab:emote_female_fail");
            IEmote mockEmote = new MockEmote(emoteURN, mockEmoteStorage) { IsLoading = true };
            mockEmote.ApplyAndMarkAsLoaded(CreateEmoteDTO(emoteURN, false));
            ((MockEmote)mockEmote).IsLoading = true;

            BodyShape bodyShape = BodyShape.FEMALE;
            var intention = new GetAssetBundleIntention { CommonArguments = new CommonLoadingArguments(URLAddress.EMPTY) };
            var exception = new Exception("Simulated AssetBundle load failure");

            Entity emoteEntity = CreateEmoteEntityWithPromise<AssetBundleData, GetAssetBundleIntention>(mockEmote, intention, bodyShape, out AssetBundlePromise promise);
            Entity resultHolderEntity = promise.Entity;

            LogAssert.Expect(LogType.Exception, $"Exception: {exception.Message}");
            world.Add(resultHolderEntity, new StreamableLoadingResult<AssetBundleData>(ReportData.UNSPECIFIED, exception));

            system.Update(0);

            Assert.IsFalse(world.IsAlive(emoteEntity), "Carrier entity should be destroyed.");
            Assert.IsFalse(world.IsAlive(resultHolderEntity), "Result-holder entity should be destroyed (even on failure).");

            Assert.IsNull(mockEmote.AssetResults[bodyShape], "Asset result should be null on failure.");
            Assert.IsFalse(mockEmote.IsLoading, "Emote loading status should be false after failure.");
        }

        [Test]
        public void FinalizeAssetBundleEmoteLoadingCancelledCorrectly()
        {
            var emoteURN = new URN("urn:ab:emote_female_cancel");
            IEmote mockEmote = new MockEmote(emoteURN, mockEmoteStorage);
            mockEmote.ApplyAndMarkAsLoaded(CreateEmoteDTO(emoteURN, false));
            ((MockEmote)mockEmote).IsLoading = true;

            BodyShape bodyShape = BodyShape.FEMALE;
            var intention = new GetAssetBundleIntention { CommonArguments = new CommonLoadingArguments(URLAddress.EMPTY) };

            Entity emoteEntity = CreateEmoteEntityWithPromise<AssetBundleData, GetAssetBundleIntention>(mockEmote, intention, bodyShape, out AssetBundlePromise promise);
            Entity resultHolderEntity = promise.Entity;

            promise.ForgetLoading(world);

            system.Update(0);

            Assert.IsFalse(world.IsAlive(emoteEntity), "Carrier entity should be destroyed on cancellation.");
            Assert.IsFalse(world.IsAlive(resultHolderEntity), "Result-holder entity should be destroyed by ForgetLoading.");

            Assert.IsNull(mockEmote.AssetResults[bodyShape], "Asset result should be null after cancellation.");
            Assert.IsFalse(mockEmote.IsLoading, "Emote loading status should be false after cancellation.");
        }

        [Test]
        public void FinalizeEmoteAudioClipLoadingCorrectly()
        {
            var emoteURN = new URN("urn:audio:emote");
            IEmote mockEmote = new MockEmote(emoteURN, mockEmoteStorage);
            BodyShape bodyShape = BodyShape.MALE;
            var intention = new GetAudioClipIntention { CommonArguments = new CommonLoadingArguments(URLAddress.EMPTY) };
            var audioClipData = new AudioClipData(null); // Mock AudioClipData

            Entity targetEntity = world.Create(mockEmote, bodyShape);
            var promise = AudioPromise.Create(world, intention, PartitionComponent.TOP_PRIORITY);
            world.Add(targetEntity, promise);
            world.Add(promise.Entity, new StreamableLoadingResult<AudioClipData>(audioClipData));

            system.Update(0);

            Assert.IsFalse(world.IsAlive(targetEntity));
            Assert.IsFalse(world.IsAlive(promise.Entity));
            Assert.IsTrue(mockEmote.AudioAssetResults[bodyShape].HasValue);
            Assert.IsTrue(mockEmote.AudioAssetResults[bodyShape].Value.Succeeded);
            Assert.AreSame(audioClipData, mockEmote.AudioAssetResults[bodyShape].Value.Asset);
        }

        [Test]
        public void FinalizeEmoteAudioClipLoadingFailsCorrectly()
        {
            var emoteURN = new URN("urn:audio:emote_fail");
            IEmote mockEmote = new MockEmote(emoteURN, mockEmoteStorage);
            BodyShape bodyShape = BodyShape.MALE;
            var intention = new GetAudioClipIntention { CommonArguments = new CommonLoadingArguments(URLAddress.EMPTY) };
            var exception = new Exception("Simulated AudioClip load failure");

            // System query: FinalizeAudioClipPromise(Entity entity, ref IEmote emote, ref AudioPromise promise, in BodyShape bodyShape)
            Entity carrierEntity = world.Create(mockEmote, bodyShape); // Entity with IEmote and BodyShape
            var promise = AudioPromise.Create(world, intention, PartitionComponent.TOP_PRIORITY);
            world.Add(carrierEntity, promise); // Add promise component to carrier
            Entity resultHolderEntity = promise.Entity;

            LogAssert.Expect(LogType.Exception, $"Exception: {exception.Message}");
            world.Add(resultHolderEntity, new StreamableLoadingResult<AudioClipData>(ReportData.UNSPECIFIED, exception));

            system.Update(0);

            Assert.IsFalse(world.IsAlive(carrierEntity), "Carrier entity should be destroyed.");
            Assert.IsFalse(world.IsAlive(resultHolderEntity), "Result-holder entity should be destroyed (even on failure).");

            Assert.IsNull(mockEmote.AudioAssetResults[bodyShape], "Audio asset result should be null on failure.");

            // IsLoading is not directly managed by FinalizeAudioClipPromise for the IEmote itself, only asset is set or not.
        }

        [Test]
        public void FinalizeEmoteAudioClipLoadingCancelledCorrectly()
        {
            var emoteURN = new URN("urn:audio:emote_cancel");
            IEmote mockEmote = new MockEmote(emoteURN, mockEmoteStorage);
            BodyShape bodyShape = BodyShape.MALE;
            var intention = new GetAudioClipIntention { CommonArguments = new CommonLoadingArguments(URLAddress.EMPTY) };

            Entity carrierEntity = world.Create(mockEmote, bodyShape);
            var promise = AudioPromise.Create(world, intention, PartitionComponent.TOP_PRIORITY);
            world.Add(carrierEntity, promise);
            Entity resultHolderEntity = promise.Entity;

            promise.ForgetLoading(world); // Destroys resultHolderEntity

            system.Update(0);

            Assert.IsFalse(world.IsAlive(carrierEntity), "Carrier entity should be destroyed on cancellation.");
            Assert.IsFalse(world.IsAlive(resultHolderEntity), "Result-holder entity should have been destroyed by ForgetLoading.");

            Assert.IsNull(mockEmote.AudioAssetResults[bodyShape], "Audio asset result should be null after cancellation.");
        }

        [Test]
        public void ConsumeAndDisposeFinishedEmotePromiseCorrectly()
        {
            var emoteURN = new URN("urn:resolution:emote");
            var pointers = new List<URN> { emoteURN };
            var intention = new GetEmotesByPointersIntention(pointers, BodyShape.MALE);
            var resolution = new EmotesResolution(RepoolableList<IEmote>.NewList(), 0);

            CancellationTokenSource cts = intention.CancellationTokenSource;
            Assert.IsFalse(cts.IsCancellationRequested, "Intention CTS should not be cancelled initially.");

            // Create the promise. promise.Entity is the entity that will hold the StreamableLoadingResult.
            var promise = EmoteResolutionPromise.Create(world, intention, PartitionComponent.TOP_PRIORITY);

            // Create a separate entity to act as the "carrier" of the EmoteResolutionPromise component.
            // This is the entity that the system's query will find and destroy.
            Entity promiseCarrierEntity = world.Create(promise);

            // Add the result to the promise's designated result-holding entity.
            world.Add(promise.Entity, new StreamableLoadingResult<EmotesResolution>(resolution));

            system.Update(0);

            // The system's query [Query]private void ConsumeAndDisposeFinishedEmotePromise(in Entity entity, ref EmotePromise promise)
            // finds 'promiseCarrierEntity' (because it has the EmotePromise component) and calls World.Destroy(entity).
            // So, promiseCarrierEntity should be destroyed.
            Assert.IsFalse(world.IsAlive(promiseCarrierEntity), "The entity carrying the promise component should be destroyed.");

            // The promise.Entity (the result holder) IS destroyed by the AssetPromise framework itself when TryConsume is called.
            Assert.IsFalse(world.IsAlive(promise.Entity), "The promise's result-holder entity should be destroyed by the AssetPromise framework upon consumption.");

            Assert.IsTrue(cts.IsCancellationRequested, "Intention CTS should be cancelled after disposal.");
        }

        private Entity CreateEmoteEntityWithPromise<TAsset, TIntention>(
            IEmote mockEmote,
            TIntention intention,
            BodyShape bodyShape,
            out AssetPromise<TAsset, TIntention> promise)
            where TIntention: struct, IAssetIntention, IEquatable<TIntention>
        {
            promise = AssetPromise<TAsset, TIntention>.Create(world, intention, PartitionComponent.TOP_PRIORITY);
            Entity targetEntity = world.Create(mockEmote, bodyShape, promise); // Promise is on the same entity as IEmote
            return targetEntity;
        }

        private EmoteDTO CreateEmoteDTO(URN urn, bool isUnisex, string name = "Test Emote") =>
            new ()
            {
                id = urn.ToString(),
                metadata = new EmoteDTO.EmoteMetadataDto
                {
                    id = urn.ToString(), // System uses metadata.id as the URN key
                    name = name,
                    emoteDataADR74 = new EmoteDTO.EmoteMetadataDto.Data
                    {
                        representations = isUnisex
                            ? new[] { AvatarAttachmentDTO.Representation.NewFakeRepresentation(), AvatarAttachmentDTO.Representation.NewFakeRepresentation() }
                            : new[] { AvatarAttachmentDTO.Representation.NewFakeRepresentation() },
                    },
                },
                content = Array.Empty<AvatarAttachmentDTO.Content>(),
            };

        public class MockStreamableDataWithURN : IStreamableRefCountData
        {
            public URN Urn { get; }

            public MockStreamableDataWithURN(URN urn)
            {
                Urn = urn;
            }

            public void Dispose() { }

            public void Dereference() { }
        }

        public class MockEmoteStorage : IEmoteStorage
        {
            public readonly Dictionary<URN, IEmote> Emotes = new ();
            public readonly List<URN> GetOrAddByDTOCalls = new ();
            public readonly List<URN> TryGetElementCalls = new ();
            public Action<MockEmote, bool> OnUpdateLoadingStatusCalled;
            public List<URN> EmbededURNs => throw new NotImplementedException();

            public IEmote GetOrAddByDTO(EmoteDTO dto, bool isDefault)
            {
                URN urn = dto.metadata.id;
                GetOrAddByDTOCalls.Add(urn);

                if (Emotes.TryGetValue(urn, out IEmote existingEmote))
                    return existingEmote;

                var newEmote = new MockEmote(urn, this);
                Emotes[urn] = newEmote;
                return newEmote;
            }

            public bool TryGetElement(URN urn, out IEmote element)
            {
                TryGetElementCalls.Add(urn);
                return Emotes.TryGetValue(urn, out element);
            }

            public void Set(URN urn, IEmote emote) =>
                Emotes[urn] = emote;

            public void AddEmbeded(URN urn, IEmote emote) =>
                throw new NotImplementedException();

            public void Unload(IPerformanceBudget budget) =>
                Emotes.Clear();

            public void SetOwnedNft(URN urn, NftBlockchainOperationEntry nft) =>
                throw new NotImplementedException();

            public bool TryGetOwnedNftRegistry(URN urn, out IReadOnlyDictionary<URN, NftBlockchainOperationEntry> registry) =>
                throw new NotImplementedException();

            public void ClearOwnedNftRegistry()
            {
                throw new NotImplementedException();
            }
        }

        public class MockEmote : IEmote
        {
            public readonly MockEmoteStorage storageRef;
            public URN Urn { get; }
            public StreamableLoadingResult<SceneAssetBundleManifest>? ManifestResult { get; set; }
            public StreamableLoadingResult<AttachmentRegularAsset>?[] AssetResults { get; }
            public StreamableLoadingResult<AudioClipData>?[] AudioAssetResults { get; }
            public EmoteDTO DTO { get; private set; }
            public bool IsLoading { get; set; }
            public int ApplyAndMarkAsLoadedCallCount { get; private set; }
            public EmoteDTO LastAppliedDTO { get; private set; }
            public bool MockIsUnisexValue { get; set; }
            public bool MockHasSameClipForAllGendersValue { get; set; }
            public StreamableLoadingResult<EmoteDTO> Model { get; set; }
            public StreamableLoadingResult<SpriteData>.WithFallback? ThumbnailAssetResult { get; set; }

            AvatarAttachmentDTO IAvatarAttachment.DTO => DTO;

            public MockEmote(URN urn, MockEmoteStorage storage = null)
            {
                Urn = urn;
                storageRef = storage;
                AssetResults = new StreamableLoadingResult<AttachmentRegularAsset>?[BodyShape.COUNT];
                AudioAssetResults = new StreamableLoadingResult<AudioClipData>?[BodyShape.COUNT];
                IsLoading = true;
            }

            public bool IsLooping() =>
                DTO?.metadata?.emoteDataADR74?.loop ?? false;

            public void UpdateLoadingStatus(bool newStatus)
            {
                IsLoading = newStatus;
                storageRef?.OnUpdateLoadingStatusCalled?.Invoke(this, newStatus);
            }

            public void ApplyAndMarkAsLoaded(EmoteDTO dto)
            {
                DTO = dto;
                IsLoading = false;
                LastAppliedDTO = dto;
                ApplyAndMarkAsLoadedCallCount++;
            }

            public bool HasSameClipForAllGenders() =>
                MockHasSameClipForAllGendersValue;

            public bool IsOnChain() =>
                Urn.ToString().StartsWith("urn:") && !Urn.ToString().StartsWith("urn:decentraland:off-chain:");
        }
    }
}
