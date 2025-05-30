using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.DTO;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using System;
using StreamableResult = ECS.StreamableLoading.Common.Components.StreamableLoadingResult<DCL.AvatarRendering.Emotes.EmotesResolution>;

namespace DCL.AvatarRendering.Emotes.Load
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.EMOTE)]
    public partial class LoadSceneEmotesSystem : BaseUnityLoopSystem
    {
        private readonly URLSubdirectory customStreamingSubdirectory;
        private readonly IEmoteStorage emoteStorage;

        public LoadSceneEmotesSystem(
            World world,
            IEmoteStorage emoteStorage,
            URLSubdirectory customStreamingSubdirectory
        )
            : base(world)
        {
            this.emoteStorage = emoteStorage;
            this.customStreamingSubdirectory = customStreamingSubdirectory;
        }

        protected override void Update(float t)
        {
            GetEmotesFromRealmQuery(World, t);
            GetEmotesFromLocalSceneQuery(World, t);
        }

        [Query]
        [None(typeof(StreamableResult))]
        private void GetEmotesFromRealm([Data] float dt, Entity entity,
            ref GetSceneEmoteFromRealmIntention intention,
            ref IPartitionComponent partitionComponent)
        {
            if (intention.TryCancelByRequest<GetSceneEmoteFromRealmIntention, EmotesResolution>(
                    World!,
                    GetReportCategory(),
                    entity,
                    static i => $"Scene emote request cancelled {i.EmoteHash}"))
                return;

            ProcessSceneEmoteIntention(dt, entity, ref intention, ref partitionComponent);
        }

        [Query]
        [None(typeof(StreamableResult))]
        private void GetEmotesFromLocalScene([Data] float dt, Entity entity,
            ref GetSceneEmoteFromLocalSceneIntention intention,
            ref IPartitionComponent partitionComponent)
        {
            ProcessSceneEmoteIntention(dt, entity, ref intention, ref partitionComponent);
        }

        private void ProcessSceneEmoteIntention<TIntention>(
            float dt,
            Entity entity,
            ref TIntention intention,
            ref IPartitionComponent partitionComponent
        ) where TIntention : struct, IEmoteAssetIntention
        {
            URN urn = intention.NewSceneEmoteURN();

            if (intention.Timeout.IsTimeout(dt))
            {
                if (!World.Has<StreamableResult>(entity))
                {
                    ReportHub.LogWarning(GetReportCategory(), $"Loading scenes emotes timed out {urn}");
                    World.Add(entity, new StreamableResult(GetReportCategory(), new TimeoutException($"Scene emote timeout {urn}")));
                }
                return;
            }

            if (!emoteStorage.TryGetElement(urn, out IEmote emote))
            {
                var dto = new EmoteDTO
                {
                    id = urn,
                    metadata = new EmoteDTO.EmoteMetadataDto
                    {
                        id = urn,
                        data = new EmoteDTO.EmoteMetadataDto.Data
                        {
                            loop = intention.Loop,
                            category = "emote",
                            hides = Array.Empty<string>(),
                            replaces = Array.Empty<string>(),
                            tags = Array.Empty<string>(),
                            removesDefaultHiding = Array.Empty<string>(),
                            representations = new AvatarAttachmentDTO.Representation[]
                            {
                                new ()
                                {
                                    contents = Array.Empty<string>(),
                                    bodyShapes = new[]
                                    {
                                        BodyShape.MALE.Value,
                                        BodyShape.FEMALE.Value,
                                    },
                                    overrideHides = Array.Empty<string>(),
                                    overrideReplaces = Array.Empty<string>(),
                                    mainFile = string.Empty,
                                },
                            },
                        },
                    },
                };

                emote = emoteStorage.GetOrAddByDTO(dto);
            }

            if (emote.IsLoading) return;

            if (CreatePromiseIfRequired(ref emote, ref intention, partitionComponent)) return;

            if (emote.AssetResults[intention.BodyShape] is { Succeeded: true })
            {
                emote.AssetResults[intention.BodyShape]?.Asset!.AddReference();
            }
            else if (intention is GetSceneEmoteFromLocalSceneIntention)
            {
                World.Add(entity, new StreamableResult(GetReportCategory(), new Exception($"Scene emote failed to load {urn}")));
                return;
            }

            World.Add(entity, new StreamableResult(new EmotesResolution(RepoolableList<IEmote>.FromElement(emote), 1)));
        }

        private bool CreatePromiseIfRequired<TIntention>(
            ref IEmote emote,
            ref TIntention intention,
            IPartitionComponent partitionComponent)
            where TIntention : struct, IEmoteAssetIntention
        {
            if (emote.AssetResults[intention.BodyShape] != null) return false;

            intention.CreateAndAddPromiseToWorld(World, partitionComponent, customStreamingSubdirectory, emote);

            emote.UpdateLoadingStatus(true);
            return true;
        }
    }
}
