using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using DCL.Diagnostics;
using DCL.SDKComponents.AudioSources;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.GLTF;
using System;

using StreamableResult = ECS.StreamableLoading.Common.Components.StreamableLoadingResult<DCL.AvatarRendering.Emotes.EmotesResolution>;
using GltfPromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.GLTF.GLTFData, ECS.StreamableLoading.GLTF.GetGLTFIntention>;

namespace DCL.AvatarRendering.Emotes.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(FinalizeEmoteLoadingSystem))]
    [LogCategory(ReportCategory.EMOTE)]
    public partial class ResolveBuilderEmotePromisesSystem : BaseUnityLoopSystem
    {
        private static readonly BodyShape[] ALL_BODYSHAPES = { BodyShape.MALE, BodyShape.FEMALE };

        private readonly IEmoteStorage emoteStorage;
        internal readonly IURLBuilder urlBuilder = new URLBuilder();

        public ResolveBuilderEmotePromisesSystem(
            World world,
            IEmoteStorage emoteStorage
            ) : base(world)
        {
            this.emoteStorage = emoteStorage;
        }

        protected override void Update(float t)
        {
            ResolveBuilderEmotePromiseQuery(World);
        }

        [Query]
        [None(typeof(StreamableResult))]
        private void ResolveBuilderEmotePromise(Entity entity, ref GetOwnedEmotesFromRealmIntention intention, ref IPartitionComponent partitionComponent)
        {
            if (intention.CancellationTokenSource.IsCancellationRequested)
            {
                World!.Add(entity, new StreamableResult(GetReportCategory(), new OperationCanceledException("Emotes request cancelled")));
                return;
            }

            // Only create promises for builder collections
            if (intention is { NeedsBuilderAPISigning: false } || intention.Result.List is not { Count: > 0 })
                return;

            bool allEmotesProcessed = true;

            foreach (IEmote emote in intention.Result.List)
            {
                if (TryCreateBuilderEmoteAssetPromises(emote, partitionComponent))
                    allEmotesProcessed = false;
            }

            if (allEmotesProcessed)
                World!.Add(entity, new StreamableResult(new EmotesResolution(intention.Result, intention.TotalAmount)));
        }

        private bool TryCreateBuilderEmoteAssetPromises(in IEmote emote, in IPartitionComponent partitionComponent)
        {
            if (string.IsNullOrEmpty(emote.DTO.ContentDownloadUrl))
                return false;

            // Check if emote already has assets loaded or is loading
            if (emote.IsLoading)
                return true; // Still processing, not done yet

            // Check if we already have this emote in storage with assets
            if (emoteStorage.TryGetElement(emote.GetUrn(), out IEmote existingEmote))
            {
                // For unisex emotes with same clip, check if either bodyshape has assets
                if (existingEmote.IsUnisex() && existingEmote.HasSameClipForAllGenders())
                {
                    if (existingEmote.AssetResults[BodyShape.MALE] != null || existingEmote.AssetResults[BodyShape.FEMALE] != null)
                        return false; // Already processed
                }
                else
                {
                    // For non-unisex emotes, check both bodyshapes
                    if (existingEmote.AssetResults[BodyShape.MALE] != null && existingEmote.AssetResults[BodyShape.FEMALE] != null)
                        return false; // Already processed
                }
            }

            bool foundGlb = false;
            bool stillProcessing = false;

            BodyShape? targetBodyShape = null;
            if (!emote.IsUnisex())
                targetBodyShape = BodyShape.FromStringSafe(emote.DTO.Metadata.AbstractData.representations[0].bodyShapes[0]);

            // The resolution of these promises will be finalized by FinalizeEmoteLoadingSystem
            foreach (var content in emote.DTO.content)
            {
                if (content.file.EndsWith(".glb"))
                {
                    for (int i = 0; i < ALL_BODYSHAPES.Length; i++)
                    {
                        BodyShape bodyShape = ALL_BODYSHAPES[i];
                        if (!emote.IsUnisex() && !bodyShape.Equals(targetBodyShape!))
                            continue;

                        // Skip if this bodyshape already has an asset result
                        if (emote.AssetResults[bodyShape] != null)
                            continue;

                        var gltfPromise = GltfPromise.Create(World!, GetGLTFIntention.Create(content.file, content.hash), partitionComponent);
                        World!.Create(gltfPromise, emote, bodyShape);
                        emote.UpdateLoadingStatus(true);
                        foundGlb = true;
                        stillProcessing = true;
                    }
                    continue;
                }

                // Supported audio format in emotes: https://docs.decentraland.org/creator/emotes/props-and-sounds/#add-audio-to-the-emotes
                if (content.file.EndsWith(".mp3") || content.file.EndsWith(".ogg"))
                {
                    var audioType = content.file.ToAudioType();
                    urlBuilder.Clear();
                    urlBuilder.AppendDomain(URLDomain.FromString(emote.DTO.ContentDownloadUrl)).AppendPath(new URLPath(content.hash));
                    URLAddress url = urlBuilder.Build();

                    for (int i = 0; i < ALL_BODYSHAPES.Length; i++)
                    {
                        BodyShape bodyShape = ALL_BODYSHAPES[i];
                        if (!emote.IsUnisex() && !bodyShape.Equals(targetBodyShape))
                            continue;

                        var audioPromise = AudioUtils.CreateAudioClipPromise(World!, url.Value, audioType, partitionComponent);
                        World!.Create(audioPromise, emote, bodyShape);
                    }

                    if (foundGlb) break;
                }
            }

            return stillProcessing;
        }
    }
}
