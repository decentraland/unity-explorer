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
using SceneRunner.Scene;
using System;
using UnityEngine;
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
        private void FinalizeGltfLoading(
            Entity entity,
            ref GltfPromise promise,
            ref IEmote emote,
            in BodyShape bodyShape)
        {
            FinalizeAssetLoading<GLTFData, GetGLTFIntention>(entity, ref promise, ref emote, bodyShape, result => result.ToRegularAsset());
        }

        [Query]
        private void FinalizeAssetBundleLoading(
            Entity entity,
            ref AssetBundlePromise promise,
            ref IEmote emote,
            in BodyShape bodyShape)
        {
            FinalizeAssetLoading<AssetBundleData, GetAssetBundleIntention>(entity, ref promise, ref emote, bodyShape, result => result.ToRegularAsset());
        }

        private void FinalizeAssetLoading<TAsset, TLoadingIntention>(
            Entity entity,
            ref AssetPromise<TAsset, TLoadingIntention> promise,
            ref IEmote emote,
            in BodyShape bodyShape,
            Func<StreamableLoadingResult<TAsset>, AttachmentRegularAsset> toRegularAsset)
            where TLoadingIntention: IAssetIntention, IEquatable<TLoadingIntention>
        {
            if (promise.LoadingIntention.CancellationTokenSource.IsCancellationRequested)
            {
                ResetEmoteResultOnCancellation(emote, bodyShape);
                promise.ForgetLoading(World);
                World.Destroy(entity);
                return;
            }

            if (promise.SafeTryConsume(World, GetReportCategory(), out StreamableLoadingResult<TAsset> result))
            {
                if (result.Succeeded)
                {
                    var asset = new StreamableLoadingResult<AttachmentRegularAsset>(toRegularAsset.Invoke(result));

                    if (emote.IsUnisex() && emote.HasSameClipForAllGenders())
                    {
                        emote.AssetResults[BodyShape.MALE] = asset;
                        emote.AssetResults[BodyShape.FEMALE] = asset;
                    }
                    else
                        emote.AssetResults[bodyShape] = asset;
                }

                emote.UpdateLoadingStatus(false);
                World.Destroy(entity);
            }
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

            ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "xx--> AUDIO PROMISE");

            if (result.Succeeded)
            {
                ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "xx--> success");

                if (emote.IsSocial)
                {
                    // Stores the audio clips of the outcomes
                    string? outcomeAudioHash = null;

                    for (int i = 0; i < emote.Model.Asset!.metadata.socialEmoteData!.outcomes!.Length; ++i)
                    {
                        ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "xx--> outcome " + i);

                        if (emote.Model.Asset!.metadata.socialEmoteData!.outcomes![i].audio != null)
                        {
                            string shape = bodyShape.Value.Contains("BaseMale") ? "male" : "female";
                            string contentName = shape + "/" + emote.Model.Asset!.metadata.socialEmoteData!.outcomes![i].audio;

                            ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "xx--> contentName " + contentName);

                            for (int j = 0; j < emote.Model.Asset.content.Length; ++j)
                            {
                                ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "xx--> comparing: " + emote.Model.Asset.content[j].file);

                                if (string.Compare(emote.Model.Asset.content[j].file, contentName, StringComparison.InvariantCultureIgnoreCase) == 0)
                                {
                                    // Found the outcome sound in the content list, we can get the hash
                                    outcomeAudioHash = emote.Model.Asset.content[j].hash;
                                    break;
                                }
                            }

                            ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "xx--> contained in? " + promise.LoadingIntention.CommonArguments.URL.Value);
                            string audioURL = promise.LoadingIntention.CommonArguments.URL.Value;

                            // If the current result corresponds to the outcome at current position...
                            if (audioURL.Contains(outcomeAudioHash!, StringComparison.InvariantCultureIgnoreCase))
                            {
                                ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "xx--> outcomeAudioHash " + outcomeAudioHash);

                                // This check is necessary because otherwise it will add more than one of each audio clip
                                bool alreadyContainsAudio = false;

                                for (int j = 0; j < emote.SocialEmoteOutcomeAudioAssetResults.Count; ++j)
                                {
                                    if (emote.SocialEmoteOutcomeAudioAssetResults[j].Asset!.Asset.name == audioURL)
                                    {
                                        alreadyContainsAudio = true;
                                        break;
                                    }
                                }

                                if (!alreadyContainsAudio)
                                {
                                    ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "xx--> ADDED" + outcomeAudioHash);

                                    result.Asset!.Asset.name = audioURL;
                                    emote.SocialEmoteOutcomeAudioAssetResults.Add(result);
                                }

                                break;
                            }
                        }
                        else
                        {
                            ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "xx--> NULL");
                            emote.SocialEmoteOutcomeAudioAssetResults.Add(new StreamableLoadingResult<AudioClipData>()); // Null audio
                        }
                    }
                }
                else
                {
                    emote.AudioAssetResults[bodyShape] = result;
                }
            }

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
