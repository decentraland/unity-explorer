using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Emotes.Load;
using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.Systems.Abstract;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.AudioClips;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.GLTF;
using System;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;
using AudioPromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AudioClips.AudioClipData, ECS.StreamableLoading.AudioClips.GetAudioClipIntention>;
using EmotesFromRealmPromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesDTOList, DCL.AvatarRendering.Emotes.GetEmotesByPointersFromRealmIntention>;
using EmotePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesResolution, DCL.AvatarRendering.Emotes.GetEmotesByPointersIntention>;
using GltfPromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.GLTF.GLTFData, ECS.StreamableLoading.GLTF.GetGLTFIntention>;

namespace DCL.AvatarRendering.Emotes
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.EMOTE)]
    [UpdateAfter(typeof(LoadEmotesByPointersSystem))]
    [UpdateAfter(typeof(LoadSceneEmotesSystem))]
    public partial class FinalizeEmoteLoadingSystem : FinalizeElementsLoadingSystem<GetEmotesByPointersFromRealmIntention, IEmote, EmoteDTO, EmotesDTOList>
    {
        public FinalizeEmoteLoadingSystem(World world, IEmoteStorage emoteStorage) : base(world, emoteStorage, new ListObjectPool<URN>()) { }

        protected override void Update(float t)
        {
            FinalizeEmoteDTOQuery(World);
            FinalizeAssetBundleLoadingQuery(World);
            FinalizeGltfLoadingQuery(World);
            FinalizeAudioClipPromiseQuery(World);
            ConsumeAndDisposeFinishedEmotePromiseQuery(World);
        }

        [Query]
        private void FinalizeEmoteDTO(
            Entity entity,
            ref EmotesFromRealmPromise promise
        )
        {
            if (TryFinalizeIfCancelled(entity, promise))
                return;

            if (promise.SafeTryConsume(World, GetReportCategory(), out StreamableLoadingResult<EmotesDTOList> promiseResult))
            {
                if (!promiseResult.Succeeded)
                {
                    foreach (var pointerID in promise.LoadingIntention.Pointers)
                        ReportAndFinalizeWithError(pointerID);
                }
                else
                    using (var list = promiseResult.Asset.ConsumeAttachments())
                        foreach (EmoteDTO assetEntity in list.Value)
                        {
                            IEmote component = storage.GetOrAddByDTO(assetEntity);
                            component.ApplyAndMarkAsLoaded(assetEntity);
                        }

                World.Destroy(entity);
            }
        }

        [Query]
        private void FinalizeAssetBundleLoading(
            Entity entity,
            ref AssetBundlePromise promise,
            ref IEmote emote)
        {
            if (IsCancellationRequested(entity, ref promise, ref emote))
                return;

            if (promise.SafeTryConsume(World, GetReportCategory(), out StreamableLoadingResult<AssetBundleData> gltfAssetResult))
            {
                if (gltfAssetResult.Succeeded && gltfAssetResult.TryToConvertToRegularAsset(out AttachmentRegularAsset regularAssetResult))
                    emote.AssetResult = new StreamableLoadingResult<AttachmentRegularAsset>(regularAssetResult);
                else
                {
                    emote.AssetResult = new StreamableLoadingResult<AttachmentRegularAsset>(GetReportData(), new Exception("LOADING ESCEPTION"));
                    ReportHub.LogWarning(GetReportData(), $"The emote {emote.DTO.id} failed to load from the AB");
                }

                emote.UpdateLoadingStatus(false);
                World.Destroy(entity);
            }
        }

        [Query]
        private void FinalizeGltfLoading(
            Entity entity,
            ref GltfPromise promise,
            ref IEmote emote)
        {
            if (IsCancellationRequested(entity, ref promise, ref emote))
                return;

            if (promise.SafeTryConsume(World, GetReportCategory(), out StreamableLoadingResult<GLTFData> gltfAssetResult))
            {
                if (gltfAssetResult.Succeeded && gltfAssetResult.TryToConvertToRegularAsset(out AttachmentRegularAsset regularAssetResult))
                    emote.AssetResult = new StreamableLoadingResult<AttachmentRegularAsset>(regularAssetResult);
                else
                    ReportHub.LogWarning(GetReportData(), $"The emote {emote.DTO.id} failed to load from the GLTF");

                emote.UpdateLoadingStatus(false);
                World.Destroy(entity);
            }
        }

        private bool IsCancellationRequested<TAsset, TLoadingIntention>(
            Entity entity,
            ref AssetPromise<TAsset, TLoadingIntention> promise,
            ref IEmote emote)
            where TLoadingIntention: IAssetIntention, IEquatable<TLoadingIntention>
        {
            if (promise.LoadingIntention.CancellationTokenSource.IsCancellationRequested)
            {
                emote.UpdateLoadingStatus(false);
                promise.ForgetLoading(World);
                World.Destroy(entity);
                return true;
            }

            return false;
        }

        [Query]
        private void FinalizeAudioClipPromise(Entity entity, ref IEmote emote, ref AudioPromise promise, in BodyShape bodyShape)
        {
            if (promise.IsCancellationRequested(World))
            {
                World.Destroy(entity);
                return;
            }

            if (promise.IsConsumed) return;

            if (!promise.SafeTryConsume(World, GetReportCategory(), out StreamableLoadingResult<AudioClipData> result))
                return;

            if (result.Succeeded)
                emote.AudioAssetResult = result;

            World.Destroy(entity);
        }

        [Query]
        private void ConsumeAndDisposeFinishedEmotePromise(in Entity entity, ref EmotePromise promise)
        {
            // The result is added into the emote storage at FinalizeEmoteDTO already, no need to do anything else
            if (!promise.SafeTryConsume(World, GetReportData(), out StreamableLoadingResult<EmotesResolution> result)) return;

            promise.LoadingIntention.Dispose();

            World.Destroy(entity);
        }

    }
}
