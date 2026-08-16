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
using SceneEmoteFromRealmPromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesResolution, DCL.AvatarRendering.Emotes.GetSceneEmoteFromRealmIntention>;
using SceneEmoteFromLocalPromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesResolution, DCL.AvatarRendering.Emotes.GetSceneEmoteFromLocalSceneIntention>;

namespace DCL.AvatarRendering.Emotes
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.EMOTE)]
    [UpdateAfter(typeof(LoadEmotesByPointersSystem))]
    [UpdateAfter(typeof(LoadSceneEmotesSystem))]
    public partial class FinalizeEmoteLoadingSystem : FinalizeElementsLoadingSystem<GetEmotesDTOByPointersFromRealmIntention, IEmote, EmoteDTO, EmotesDTOList>
    {
        public FinalizeEmoteLoadingSystem(World world, IEmoteStorage emoteStorage) : base(world, emoteStorage, new ListObjectPool<URN>()) { }

        protected override void Update(float t)
        {
            FinalizeEmoteDTOQuery(World);
            FinalizeAssetBundleLoadingQuery(World);
            FinalizeGltfLoadingQuery(World);
            FinalizeAudioClipPromiseQuery(World);
            ConsumeAndDisposeFinishedEmotePromiseQuery(World);
            ConsumeAndDisposeFinishedSceneEmoteFromRealmPromiseQuery(World);
            ConsumeAndDisposeFinishedSceneEmoteFromLocalPromiseQuery(World);
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
            // The loaded emotes stay owned by the emote storage (added at FinalizeEmoteDTO); the promise only
            // has to return the reference LoadEmotesByPointersSystem added per successful pointer, so the
            // storage can unload the assets once nothing plays them.
            if (!promise.SafeTryConsume(World, GetReportData(), out StreamableLoadingResult<EmotesResolution> result)) return;

            foreach (URN urn in promise.LoadingIntention.SuccessfulPointers)
            {
                if (!storage.TryGetElement(urn, out IEmote emote)) continue;

                emote.AssetResults[promise.LoadingIntention.BodyShape]?.Asset?.Dereference();
            }

            if (result.Succeeded)
                result.Asset.ConsumeEmotes().Dispose();

            promise.LoadingIntention.Dispose();

            World.Destroy(entity);
        }

        [Query]
        private void ConsumeAndDisposeFinishedSceneEmoteFromRealmPromise(in Entity entity, ref SceneEmoteFromRealmPromise promise)
        {
            if (!promise.SafeTryConsume(World, GetReportData(), out StreamableLoadingResult<EmotesResolution> result)) return;

            DereferenceSceneEmote(promise.LoadingIntention.NewSceneEmoteURN(), promise.LoadingIntention.BodyShape, in result);

            World.Destroy(entity);
        }

        [Query]
        private void ConsumeAndDisposeFinishedSceneEmoteFromLocalPromise(in Entity entity, ref SceneEmoteFromLocalPromise promise)
        {
            if (!promise.SafeTryConsume(World, GetReportData(), out StreamableLoadingResult<EmotesResolution> result)) return;

            DereferenceSceneEmote(promise.LoadingIntention.NewSceneEmoteURN(), promise.LoadingIntention.BodyShape, in result);

            World.Destroy(entity);
        }

        /// <summary>
        ///     Returns the reference LoadSceneEmotesSystem adds when it resolves an intention whose asset
        ///     loaded successfully; the guard must mirror that condition exactly or the count goes negative.
        /// </summary>
        private void DereferenceSceneEmote(URN urn, BodyShape bodyShape, in StreamableLoadingResult<EmotesResolution> result)
        {
            if (!result.Succeeded) return;

            if (storage.TryGetElement(urn, out IEmote emote) && emote.AssetResults[bodyShape] is { Succeeded: true } assetResult)
                assetResult.Asset?.Dereference();

            result.Asset.ConsumeEmotes().Dispose();
        }

        private static void ResetEmoteResultOnCancellation(IEmote emote, in BodyShape bodyShape)
        {
            emote.UpdateLoadingStatus(false);

            if (emote.AssetResults[bodyShape] is { IsInitialized: false })
                emote.AssetResults[bodyShape] = null;
        }
    }
}
