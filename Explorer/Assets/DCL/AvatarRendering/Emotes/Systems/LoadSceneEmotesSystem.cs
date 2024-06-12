using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using System;
using UnityEngine;
using Utility;
using StreamableResult = ECS.StreamableLoading.Common.Components.StreamableLoadingResult<DCL.AvatarRendering.Emotes.EmotesResolution>;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;

namespace DCL.AvatarRendering.Emotes
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.EMOTE)]
    public partial class LoadSceneEmotesSystem : BaseUnityLoopSystem
    {
        private const string SCENE_EMOTE_PREFIX = "urn:decentraland:off-chain:scene-emote";

        private readonly URLSubdirectory customStreamingSubdirectory;
        private readonly IEmoteCache emoteCache;

        public LoadSceneEmotesSystem(World world,
            IEmoteCache emoteCache,
            URLSubdirectory customStreamingSubdirectory)
            : base(world)
        {
            this.emoteCache = emoteCache;
            this.customStreamingSubdirectory = customStreamingSubdirectory;
        }

        protected override void Update(float t)
        {
            GetEmotesFromRealmQuery(World, t);
        }

        [Query]
        [None(typeof(StreamableResult))]
        private void GetEmotesFromRealm([Data] float dt, in Entity entity,
            ref GetSceneEmoteFromRealmIntention intention,
            ref IPartitionComponent partitionComponent)
        {
            if (intention.CancellationTokenSource.IsCancellationRequested)
            {
                if (!World.Has<StreamableResult>(entity))
                    World.Add(entity, new StreamableResult(new OperationCanceledException($"Scene emote request cancelled {intention.Hash}")));

                return;
            }

            intention.ElapsedTime += dt;

            URN urn = GetUrn(intention.Hash, intention.Loop);

            if (!emoteCache.TryGetEmote(urn, out IEmote emote))
            {
                if (!intention.IsModelProcessed)
                {
                    var dto = new EmoteDTO
                    {
                        id = urn,
                        metadata = new EmoteDTO.Metadata
                        {
                            id = urn,
                            emoteDataADR74 = new EmoteDTO.Metadata.Data
                            {
                                loop = intention.Loop,
                                category = "scene",
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
                                            BodyShape.MALE.ToString(),
                                            BodyShape.FEMALE.ToString(),
                                        },
                                        overrideHides = Array.Empty<string>(),
                                        overrideReplaces = Array.Empty<string>(),
                                        mainFile = "",
                                    },
                                },
                            },
                        },
                    };

                    emote = new Emote
                    {
                        Model = new StreamableLoadingResult<EmoteDTO>(dto),
                        IsLoading = false,
                    };

                    emoteCache.Set(urn, emote);

                    intention.IsModelProcessed = true;
                }
            }

            if (emote.IsLoading) return;
            if (CreateAssetBundlePromiseIfRequired(emote, in intention, partitionComponent)) return;

            if (emote.AssetResults[intention.BodyShape] != null && !intention.IsAssetBundleProcessed)
            {
                // TODO: it may occur that the requested emote does not support the body shape
                // If that is the case, the promise will never be resolved
                intention.IsAssetBundleProcessed = true;

                if (emote.AssetResults[intention.BodyShape] is { Succeeded: true })
                {
                    // We need to add a reference here, so it is not lost if the flow interrupts in between (i.e. before creating instances of CachedWearable)
                    emote.AssetResults[intention.BodyShape]?.Asset!.AddReference();
                }
            }

            if (!intention.IsAssetBundleProcessed) return;

            bool isTimeout = intention.ElapsedTime >= intention.Timeout;

            if (isTimeout)
            {
                ReportHub.LogWarning(GetReportCategory(), $"Loading scenes emotes timed out {urn}");

                if (!World.Has<StreamableResult>(entity))
                    World.Add(entity, new StreamableResult(new TimeoutException($"Scene emote timeout {intention.Hash}")));

                return;
            }

            World.Add(entity, new StreamableResult(new EmotesResolution(new[] { emote }, 1)));
        }

        // This is solved by LoadEmotesByPointersSystem
        // [Query]
        // private void FinalizeAssetBundleManifestLoading(in Entity entity, ref AssetBundleManifestPromise promise,
        //     ref IEmote emote)
        // {
        //     if (promise.LoadingIntention.CancellationTokenSource.IsCancellationRequested)
        //     {
        //         emote.ManifestResult = null;
        //         emote.IsLoading = false;
        //         promise.ForgetLoading(World);
        //         World.Destroy(entity);
        //         return;
        //     }
        //
        //     if (promise.SafeTryConsume(World, out StreamableLoadingResult<SceneAssetBundleManifest> result))
        //     {
        //         emote.ManifestResult = result;
        //         emote.IsLoading = false;
        //         World.Destroy(entity);
        //     }
        // }

        // This is solved by LoadEmotesByPointersSystem
        // [Query]
        // private void FinalizeAssetBundleLoading(in Entity entity, ref AssetBundlePromise promise, ref IEmote emote, ref BodyShape bodyShape)
        // {
        //     if (promise.LoadingIntention.CancellationTokenSource.IsCancellationRequested)
        //     {
        //         ResetEmoteResultOnCancellation(emote, bodyShape);
        //         promise.ForgetLoading(World);
        //         World.Destroy(entity);
        //         return;
        //     }
        //
        //     if (promise.SafeTryConsume(World, out StreamableLoadingResult<AssetBundleData> result))
        //     {
        //         if (result.Succeeded)
        //         {
        //             var asset = new StreamableLoadingResult<WearableRegularAsset>(result.ToRegularAsset());
        //
        //             if (emote.IsUnisex())
        //             {
        //                 // TODO: can an emote have different files for each gender?
        //                 // if that the case, we should not set the same asset result for both body shapes
        //                 emote.AssetResults[BodyShape.MALE] = asset;
        //                 emote.AssetResults[BodyShape.FEMALE] = asset;
        //             }
        //             else
        //                 emote.AssetResults[bodyShape] = asset;
        //         }
        //
        //         emote.IsLoading = false;
        //         World.Destroy(entity);
        //     }
        // }

        // This is solved by LoadEmotesByPointersSystem
        // [Query]
        // private void FinalizeAudioClipPromise(in Entity entity, ref IEmote emote, ref AudioPromise promise, BodyShape bodyShape)
        // {
        //     if (promise.LoadingIntention.CancellationTokenSource.IsCancellationRequested)
        //     {
        //         promise.ForgetLoading(World);
        //         World.Destroy(entity);
        //         return;
        //     }
        //
        //     if (promise.IsConsumed) return;
        //
        //     if (!promise.SafeTryConsume(World, out StreamableLoadingResult<AudioClip> result))
        //         return;
        //
        //     if (result.Succeeded)
        //         emote.AudioAssetResults[bodyShape] = result;
        //
        //     World.Destroy(entity);
        // }

        private bool CreateAssetBundlePromiseIfRequired(IEmote emote, in GetSceneEmoteFromRealmIntention intention, IPartitionComponent partitionComponent)
        {
            if (emote.AssetResults[intention.BodyShape] != null) return false;

            var promise = AssetBundlePromise.Create(World,
                GetAssetBundleIntention.FromHash(typeof(GameObject),
                    intention.Hash + PlatformUtils.GetPlatform(),
                    permittedSources: intention.PermittedSources,
                    customEmbeddedSubDirectory: customStreamingSubdirectory,
                    cancellationTokenSource: intention.CancellationTokenSource,
                    manifest: intention.AssetBundleManifest),
                partitionComponent);

            emote.IsLoading = true;
            World.Create(promise, emote, intention.BodyShape);

            return true;
        }

        private static URN GetUrn(string hash, bool loop) =>
            new ($"{SCENE_EMOTE_PREFIX}:{hash}-{loop.ToString().ToLower()}");
    }
}
