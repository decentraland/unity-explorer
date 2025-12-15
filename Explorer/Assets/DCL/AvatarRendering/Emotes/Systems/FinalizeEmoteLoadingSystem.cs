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
                    ReportHub.LogWarning(GetReportData(), $"The emote {emote.DTO.id} failed to load from the AB");

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
                    ReportHub.LogWarning(GetReportData(), $"The emote {emote.DTO.id} failed to load from the GLTF");

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
            {
                string audioURL = promise.LoadingIntention.CommonArguments.URL.Value;

                ReportHub.Log(ReportCategory.SOCIAL_EMOTE, "FinalizeAudioClipPromise() Audio URL: " + audioURL + " body: " + bodyShape.Value);

                if (emote.IsSocial)
                {
                    if (emote.SocialEmoteOutcomeAudioAssetResults == null)
                    {
                        emote.SocialEmoteOutcomeAudioAssetResults = new StreamableLoadingResult<AudioClipData>?[emote.Model.Asset!.metadata.data!.outcomes!.Length];
                    }

                    // Stores the audio clips of the outcomes
                    for (int i = 0; i < emote.Model.Asset!.metadata.data!.outcomes!.Length; ++i)
                    {
                        // Several outcomes may have the same audio, in order to avoid setting the same outcome always we skip the already filled slots
                        if (emote.SocialEmoteOutcomeAudioAssetResults[i].HasValue)
                        {
                            ReportHub.Log(ReportCategory.SOCIAL_EMOTE, "FinalizeAudioClipPromise() Next iteration " + i);
                            continue;
                        }

                        if (emote.Model.Asset!.metadata.data!.outcomes![i].audio != null)
                        {
                            string? outcomeAudioHash = FindAudioFileHashInContent(emote, bodyShape, emote.Model.Asset!.metadata.data!.outcomes![i].audio);

                            ReportHub.Log(ReportCategory.SOCIAL_EMOTE, "FinalizeAudioClipPromise() outcome audio hash " + outcomeAudioHash);

                            // If the current result corresponds to the outcome at current position...
                            if (audioURL.Contains(outcomeAudioHash!, StringComparison.InvariantCultureIgnoreCase))
                            {
                                ReportHub.Log(ReportCategory.SOCIAL_EMOTE, $"FinalizeAudioClipPromise() Added outcome audio Hash: {outcomeAudioHash} File: {emote.Model.Asset!.metadata.data!.outcomes![i].audio}");

                                // Stores outcome audio
                                result.Asset!.Asset.name = audioURL;
                                emote.SocialEmoteOutcomeAudioAssetResults[i] = result;

                                break;
                            }
                        }
                    }

                    // Stores the audio clip of the start animation
                    if (emote.Model.Asset!.metadata.data!.startAnimation!.audio != null)
                    {
                        string? audioHash = FindAudioFileHashInContent(emote, bodyShape, emote.Model.Asset!.metadata.data!.startAnimation!.audio);

                        ReportHub.Log(ReportCategory.SOCIAL_EMOTE, "FinalizeAudioClipPromise() start audio hash " + audioHash);

                        // If the current result corresponds to the start animation...
                        if (audioHash != null && audioURL.Contains(audioHash, StringComparison.InvariantCultureIgnoreCase))
                        {
                            ReportHub.Log(ReportCategory.SOCIAL_EMOTE, "FinalizeAudioClipPromise() Added start audio " + audioHash);

                            result.Asset!.Asset.name = audioURL;
                            emote.AudioAssetResults[bodyShape] = result;
                        }
                    }
                }
                else
                {
                    ReportHub.Log(ReportCategory.SOCIAL_EMOTE, "FinalizeAudioClipPromise() normal");
                    emote.AudioAssetResults[bodyShape] = result;
                }
            }

            World.Destroy(entity);
        }

        private string? FindAudioFileHashInContent(IEmote emote, in BodyShape bodyShape, string? audioFileName)
        {
            string? audioHash = null;
            string shape = bodyShape.Value.Contains("BaseMale") ? "male" : "female";
            string contentName = shape + "/" + audioFileName;

            for (int i = 0; i < emote.Model.Asset!.content.Length; ++i)
            {
                if (string.Compare(emote.Model.Asset.content[i].file, contentName, StringComparison.InvariantCultureIgnoreCase) == 0)
                {
                    // Found the outcome sound in the content list, we can get the hash
                    audioHash = emote.Model.Asset.content[i].hash;
                    break;
                }
            }

            return audioHash;
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
