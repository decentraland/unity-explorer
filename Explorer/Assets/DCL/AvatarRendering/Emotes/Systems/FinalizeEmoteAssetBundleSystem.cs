using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.Wearables;
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

namespace DCL.AvatarRendering.Emotes
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.EMOTE)]
    [UpdateAfter(typeof(LoadEmotesByPointersSystem))]
    [UpdateAfter(typeof(LoadSceneEmotesSystem))]
    public partial class FinalizeEmoteAssetBundleSystem : BaseUnityLoopSystem
    {
        public FinalizeEmoteAssetBundleSystem(World world) : base(world)
        {
        }

        protected override void Update(float t)
        {
            FinalizeAssetBundleManifestLoadingQuery(World);
            FinalizeAssetBundleLoadingQuery(World);
            FinalizeAudioClipPromiseQuery(World);
        }

        [Query]
        private void FinalizeAssetBundleManifestLoading(in Entity entity, ref AssetBundleManifestPromise promise,
            ref IEmote emote)
        {
            if (promise.LoadingIntention.CancellationTokenSource.IsCancellationRequested)
            {
                emote.ManifestResult = null;
                emote.IsLoading = false;
                promise.ForgetLoading(World);
                World.Destroy(entity);
                return;
            }

            if (promise.SafeTryConsume(World, out StreamableLoadingResult<SceneAssetBundleManifest> result))
            {
                emote.ManifestResult = result;
                emote.IsLoading = false;
                World.Destroy(entity);
            }
        }

        [Query]
        private void FinalizeAssetBundleLoading(in Entity entity, ref AssetBundlePromise promise, ref IEmote emote, ref BodyShape bodyShape)
        {
            if (promise.LoadingIntention.CancellationTokenSource.IsCancellationRequested)
            {
                ResetEmoteResultOnCancellation(emote, bodyShape);
                promise.ForgetLoading(World);
                World.Destroy(entity);
                return;
            }

            if (promise.SafeTryConsume(World, out StreamableLoadingResult<AssetBundleData> result))
            {
                if (result.Succeeded)
                {
                    var asset = new StreamableLoadingResult<WearableRegularAsset>(result.ToRegularAsset());

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

                emote.IsLoading = false;
                World.Destroy(entity);
            }
        }

        [Query]
        private void FinalizeAudioClipPromise(in Entity entity, ref IEmote emote, ref AudioPromise promise, BodyShape bodyShape)
        {
            if (promise.LoadingIntention.CancellationTokenSource.IsCancellationRequested)
            {
                promise.ForgetLoading(World);
                World.Destroy(entity);
                return;
            }

            if (promise.IsConsumed) return;

            if (!promise.SafeTryConsume(World, out StreamableLoadingResult<AudioClip> result))
                return;

            if (result.Succeeded)
                emote.AudioAssetResults[bodyShape] = result;

            World.Destroy(entity);
        }

        private static void ResetEmoteResultOnCancellation(IEmote emote, BodyShape bodyShape)
        {
            emote.IsLoading = false;

            if (emote.AssetResults[bodyShape] is { IsInitialized: false })
                emote.AssetResults[bodyShape] = null;
        }
    }
}
