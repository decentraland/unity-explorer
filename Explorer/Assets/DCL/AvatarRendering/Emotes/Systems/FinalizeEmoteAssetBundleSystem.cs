using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using UnityEngine;
using AssetBundleManifestPromise = ECS.StreamableLoading.Common.AssetPromise<SceneRunner.Scene.SceneAssetBundleManifest, DCL.AvatarRendering.Wearables.Components.GetWearableAssetBundleManifestIntention>;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;
using AudioPromise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.AudioClip, ECS.StreamableLoading.AudioClips.GetAudioClipIntention>;
using EmotesFromRealmPromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesDTOList,
    DCL.AvatarRendering.Emotes.GetEmotesByPointersFromRealmIntention>;

namespace DCL.AvatarRendering.Emotes
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.EMOTE)]
    [UpdateAfter(typeof(Load.LoadEmotesByPointersSystem))]
    [UpdateAfter(typeof(Load.LoadSceneEmotesSystem))]
    public partial class FinalizeEmoteAssetBundleSystem : BaseUnityLoopSystem
    {
        private readonly IEmoteCache emoteCache;

        public FinalizeEmoteAssetBundleSystem(World world, IEmoteCache emoteCache) : base(world)
        {
            this.emoteCache = emoteCache;
        }

        protected override void Update(float t)
        {
            FinalizeEmoteDTOQuery(World!);
            FinalizeAssetBundleManifestLoadingQuery(World);
            FinalizeAssetBundleLoadingQuery(World);
            FinalizeAudioClipPromiseQuery(World);
        }

        [Query]
        private void FinalizeAssetBundleManifestLoading(Entity entity, ref AssetBundleManifestPromise promise,
            ref IEmote emote)
        {
            if (promise.TryForgetWithEntityIfCancelled(entity, World!))
            {
                emote.ResetManifest();
                return;
            }

            if (promise.SafeTryConsume(World!, out StreamableLoadingResult<SceneAssetBundleManifest> result))
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

            if (promise.SafeTryConsume(World!, out StreamableLoadingResult<EmotesDTOList> promiseResult))
            {
                if (!promiseResult.Succeeded)
                {
                    foreach (var pointerID in promise.LoadingIntention.Pointers)
                        if (emoteCache.TryGetElement(pointerID, out IEmote component))
                            component.UpdateLoadingStatus(false);
                }
                else
                    foreach (EmoteDTO assetEntity in promiseResult.Asset.Value)
                    {
                        IEmote component = emoteCache.GetOrAddByDTO(assetEntity);
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

            if (promise.SafeTryConsume(World, out StreamableLoadingResult<AssetBundleData> result))
            {
                if (result.Succeeded)
                {
                    var asset = new StreamableLoadingResult<AttachmentRegularAsset>(result.ToRegularAsset());

                    if (emote.IsUnisex())
                    {
                        // TODO: can an emote have different files for each gender?
                        // if that the case, we should not set the same asset result for both body shapes
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

            if (!promise.SafeTryConsume(World!, out StreamableLoadingResult<AudioClip> result))
                return;

            if (result.Succeeded)
                emote.AudioAssetResults[bodyShape] = result;

            World!.Destroy(entity);
        }

        private static void ResetEmoteResultOnCancellation(IEmote emote, BodyShape bodyShape)
        {
            emote.UpdateLoadingStatus(false);

            if (emote.AssetResults[bodyShape] is { IsInitialized: false })
                emote.AssetResults[bodyShape] = null;
        }
    }
}
