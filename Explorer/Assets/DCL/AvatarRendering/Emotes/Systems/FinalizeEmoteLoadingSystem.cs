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
using EmotesFromRealmPromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesDTOList, DCL.AvatarRendering.Emotes.GetEmotesDTOByPointersFromRealmIntention>;
using EmotePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesResolution, DCL.AvatarRendering.Emotes.GetEmotesByPointersIntention>;
using GltfPromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.GLTF.GLTFData, ECS.StreamableLoading.GLTF.GetGLTFIntention>;

namespace DCL.AvatarRendering.Emotes
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.EMOTE)]
    [UpdateAfter(typeof(LoadEmotesByPointersSystem))]
    [UpdateAfter(typeof(LoadSceneEmotesSystem))]
    public partial class FinalizeEmoteLoadingSystem : FinalizeElementsLoadingSystem<GetEmotesDTOByPointersFromRealmIntention, IEmote, EmoteDTO, EmotesDTOList>
    {
        private readonly AbgenConversionMetrics abgenConversionMetrics;

        public FinalizeEmoteLoadingSystem(World world, IEmoteStorage emoteStorage, AbgenConversionMetrics abgenConversionMetrics) : base(world, emoteStorage, new ListObjectPool<URN>())
        {
            this.abgenConversionMetrics = abgenConversionMetrics;
        }

        protected override void Update(float t)
        {
            FinalizeEmoteDTOQuery(World);
            FinalizeAssetBundleLoadingQuery(World);
            FinalizeGltfLoadingQuery(World);
            FinalizeAudioClipPromiseQuery(World);
            ConsumeAndDisposeFinishedEmotePromiseQuery(World);
        }

        [Query]
        private void FinalizeEmoteDTO(Entity entity, ref EmotesFromRealmPromise promise)
        {
            if (TryFinalizeIfCancelled(entity, ref promise))
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

                promise.LoadingIntention.ReleasePointers();
                World.Destroy(entity);
            }
        }

        [Query]
        private void FinalizeAssetBundleLoading(
            Entity entity,
            ref AssetBundlePromise promise,
            ref IEmote emote,
            in BodyShape bodyShape)
        {
            if (IsCancellationRequested(entity, ref promise, ref emote, in bodyShape))
                return;

            if (promise.SafeTryConsume(World, GetReportCategory(), out StreamableLoadingResult<AssetBundleData> gltfAssetResult))
            {
                if (gltfAssetResult.Succeeded && gltfAssetResult.TryToConvertToRegularAsset(out AttachmentRegularAsset regularAssetResult))
                    AssignEmoteResult(emote, bodyShape, regularAssetResult);
                else if (World.TryGet(entity, out SceneEmoteRawGltfFallback fallback))
                {
                    // The bundle is cached and reference-counted; this consumer is leaving it for the raw file.
                    if (gltfAssetResult.Succeeded)
                        gltfAssetResult.Asset?.Dereference();

                    FallBackToRawGltf(entity, emote, bodyShape, fallback, gltfAssetResult.Succeeded ? "bundle has no GameObject" : gltfAssetResult.Exception?.Message ?? "bundle failed to load");
                    return;
                }
                else
                {
                    ReportHub.LogWarning(GetReportData(), $"The emote {emote.DTO.id} failed to load from the AB");
                    AssignFailedEmoteResult(emote, bodyShape);
                }

                emote.UpdateLoadingStatus(false);
                World.Destroy(entity);
            }
        }

        [Query]
        private void FinalizeGltfLoading(
            Entity entity,
            ref GltfPromise promise,
            ref IEmote emote,
            in BodyShape bodyShape)
        {
            if (IsCancellationRequested(entity, ref promise, ref emote, in bodyShape))
                return;

            if (promise.SafeTryConsume(World, GetReportCategory(), out StreamableLoadingResult<GLTFData> gltfAssetResult))
            {
                if (gltfAssetResult.Succeeded && gltfAssetResult.TryToConvertToRegularAsset(out AttachmentRegularAsset regularAssetResult))
                    AssignEmoteResult(emote, bodyShape, regularAssetResult);
                else
                {
                    ReportHub.LogWarning(GetReportData(), $"The emote {emote.DTO.id} failed to load from the GLTF");
                    AssignFailedEmoteResult(emote, bodyShape);
                }

                emote.UpdateLoadingStatus(false);
                World.Destroy(entity);
            }
        }

        /// <summary>
        ///     Replaces the failed bundle promise with a raw GLB promise for the same emote and body shape; the emote stays loading.
        /// </summary>
        private void FallBackToRawGltf(Entity entity, IEmote emote, BodyShape bodyShape, SceneEmoteRawGltfFallback fallback, string reason)
        {
            ReportHub.LogWarning(GetReportData(), $"{fallback.EmotePath}: scene emote asset bundle unavailable ({reason}), loading the raw GLB instead");
            abgenConversionMetrics.OnFailed(fallback.EmotePath, $"{reason} — loaded as raw GLB");

            World.Destroy(entity);
            GetSceneEmoteFromLocalSceneIntention.CreateRawGltfPromise(World, fallback.EmotePath, fallback.EmoteHash, fallback.Partition, emote, bodyShape);
        }

        private void AssignEmoteResult(IEmote emote, BodyShape bodyShape, AttachmentRegularAsset regularAssetResult)
        {
            var asset = new StreamableLoadingResult<AttachmentRegularAsset>(regularAssetResult);

            if (emote.IsUnisex() && emote.HasSameClipForAllGenders())
            {
                emote.AssetResults[BodyShape.MALE] = asset;
                emote.AssetResults[BodyShape.FEMALE] = asset;
            }
            else
                emote.AssetResults[bodyShape] = asset;
        }

        private void AssignFailedEmoteResult(IEmote emote, BodyShape bodyShape)
        {
            var failedResult = new StreamableLoadingResult<AttachmentRegularAsset>(
                GetReportData(),
                new Exception($"Emote {emote.DTO.id} failed to load"));

            if (emote.IsUnisex() && emote.HasSameClipForAllGenders())
            {
                emote.AssetResults[BodyShape.MALE] = failedResult;
                emote.AssetResults[BodyShape.FEMALE] = failedResult;
            }
            else
                emote.AssetResults[bodyShape] = failedResult;
        }

        private bool IsCancellationRequested<TAsset, TLoadingIntention>(
            Entity entity,
            ref AssetPromise<TAsset, TLoadingIntention> promise,
            ref IEmote emote,
            in BodyShape bodyShape)
            where TLoadingIntention: IAssetIntention, IEquatable<TLoadingIntention>
        {
            if (promise.LoadingIntention.CancellationTokenSource.IsCancellationRequested)
            {
                ResetEmoteResultOnCancellation(emote, bodyShape);
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
                emote.AudioAssetResults[bodyShape] = result;

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

        private static void ResetEmoteResultOnCancellation(IEmote emote, in BodyShape bodyShape)
        {
            emote.UpdateLoadingStatus(false);

            if (emote.AssetResults[bodyShape] is { IsInitialized: false })
                emote.AssetResults[bodyShape] = null;
        }
    }
}
