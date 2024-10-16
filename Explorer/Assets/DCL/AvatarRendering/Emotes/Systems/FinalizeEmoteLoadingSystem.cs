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
using SceneRunner.Scene;
using AssetBundleManifestPromise = ECS.StreamableLoading.Common.AssetPromise<SceneRunner.Scene.SceneAssetBundleManifest, DCL.AvatarRendering.Wearables.Components.GetWearableAssetBundleManifestIntention>;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;
using AudioPromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AudioClips.AudioClipData, ECS.StreamableLoading.AudioClips.GetAudioClipIntention>;
using EmotesFromRealmPromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesDTOList,
    DCL.AvatarRendering.Emotes.GetEmotesByPointersFromRealmIntention>;
using EmotePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesResolution,
    DCL.AvatarRendering.Emotes.GetEmotesByPointersIntention>;

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
            FinalizeEmoteDTOQuery(World!);
            FinalizeAssetBundleManifestLoadingQuery(World);
            FinalizeAssetBundleLoadingQuery(World);
            FinalizeAudioClipPromiseQuery(World);
            ConsumeAndDisposeFinishedEmotePromiseQuery(World);
        }

        [Query]
        private void FinalizeAssetBundleManifestLoading(
            Entity entity,
            ref AssetBundleManifestPromise promise,
            ref IEmote emote
        )
        {
            if (promise.TryForgetWithEntityIfCancelled(entity, World!))
            {
                emote.ResetManifest();
                return;
            }

            if (promise.SafeTryConsume(World!, GetReportCategory(), out StreamableLoadingResult<SceneAssetBundleManifest> result))
            {
                emote.UpdateManifest(result);
                World!.Destroy(entity);
            }
        }

        [Query]
        private void FinalizeEmoteDTO(
            Entity entity,
            ref EmotesFromRealmPromise promise
        )
        {
            if (promise.TryForgetWithEntityIfCancelled(entity, World!))
                return;

            if (promise.SafeTryConsume(World!, GetReportCategory(), out StreamableLoadingResult<EmotesDTOList> promiseResult))
            {
                if (!promiseResult.Succeeded)
                {
                    foreach (var pointerID in promise.LoadingIntention.Pointers)
                        if (storage.TryGetElement(pointerID, out IEmote component))
                            component.UpdateLoadingStatus(false);
                }
                else
                    using (var list = promiseResult.Asset.ConsumeAttachments())
                        foreach (EmoteDTO assetEntity in list.Value)
                        {
                            IEmote component = storage.GetOrAddByDTO(assetEntity);
                            component.ApplyAndMarkAsLoaded(assetEntity);
                        }

                World!.Destroy(entity);
            }
        }

        [Query]
        private void FinalizeAssetBundleLoading(Entity entity, ref AssetBundlePromise promise, ref IEmote emote, ref BodyShape bodyShape)
        {
            if (promise.TryForgetWithEntityIfCancelled(entity, World!))
            {
                ResetEmoteResultOnCancellation(emote, bodyShape);
                return;
            }

            if (promise.SafeTryConsume(World, GetReportCategory(), out StreamableLoadingResult<AssetBundleData> result))
            {
                if (result.Succeeded)
                {
                    var asset = new StreamableLoadingResult<AttachmentRegularAsset>(result.ToRegularAsset());

                    if (emote.IsUnisex() && emote.HasSameClipForAllGenders())
                    {
                        emote.AssetResults[BodyShape.MALE] = asset;
                        emote.AssetResults[BodyShape.FEMALE] = asset;
                    }
                    else
                        emote.AssetResults[bodyShape] = asset;
                }

                emote.UpdateLoadingStatus(false);
                World!.Destroy(entity);
            }
        }

        [Query]
        private void FinalizeAudioClipPromise(Entity entity, ref IEmote emote, ref AudioPromise promise, BodyShape bodyShape)
        {
            if (promise.TryForgetWithEntityIfCancelled(entity, World!))
                return;

            if (promise.IsConsumed) return;

            if (!promise.SafeTryConsume(World!, GetReportCategory(), out StreamableLoadingResult<AudioClipData> result))
                return;

            if (result.Succeeded)
                emote.AudioAssetResults[bodyShape] = result;

            World!.Destroy(entity);
        }

        [Query]
        private void ConsumeAndDisposeFinishedEmotePromise(in Entity entity, ref EmotePromise promise)
        {
            // The result is added into the emote storage at FinalizeEmoteDTO already, no need to do anything else
            if (!promise.SafeTryConsume(World, GetReportData(), out StreamableLoadingResult<EmotesResolution> result)) return;

            promise.LoadingIntention.Dispose();

            World.Destroy(entity);
        }

        private static void ResetEmoteResultOnCancellation(IEmote emote, BodyShape bodyShape)
        {
            emote.UpdateLoadingStatus(false);

            if (emote.AssetResults[bodyShape] is { IsInitialized: false })
                emote.AssetResults[bodyShape] = null;
        }
    }
}
