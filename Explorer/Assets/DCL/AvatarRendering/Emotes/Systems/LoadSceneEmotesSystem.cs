using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using AssetManagement;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables;
using DCL.Diagnostics;
using DCL.SDKComponents.AudioSources;
using ECS;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System;
using UnityEngine;
using Utility;
using StreamableResult = ECS.StreamableLoading.Common.Components.StreamableLoadingResult<DCL.AvatarRendering.Emotes.EmotesResolution>;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;
using AudioPromise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.AudioClip, ECS.StreamableLoading.AudioClips.GetAudioClipIntention>;

namespace DCL.AvatarRendering.Emotes
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.EMOTE)]
    public partial class LoadSceneEmotesSystem : BaseUnityLoopSystem
    {
        private const string SCENE_EMOTE_PREFIX = "urn:decentraland:off-chain:scene-emote";

        private readonly URLSubdirectory customStreamingSubdirectory;
        private readonly IEmoteCache emoteCache;
        private readonly IRealmData realmData;
        private readonly URLBuilder urlBuilder;

        public LoadSceneEmotesSystem(World world,
            IEmoteCache emoteCache,
            IRealmData realmData,
            URLSubdirectory customStreamingSubdirectory)
            : base(world)
        {
            this.emoteCache = emoteCache;
            this.realmData = realmData;
            this.customStreamingSubdirectory = customStreamingSubdirectory;
            urlBuilder = new URLBuilder();
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
                    World.Add(entity, new StreamableResult(new OperationCanceledException("Pointer request cancelled")));

                return;
            }

            intention.ElapsedTime += dt;

            URN urn = GetUrn(intention.Hash, intention.Loop);

            if (!emoteCache.TryGetEmote(urn, out IEmote emote))
            {
                if (!intention.IsModelProcessed)
                {
                    emote = new Emote
                    {
                        Model = new StreamableLoadingResult<EmoteDTO>(new EmoteDTO
                        {
                            id = urn,
                        }),
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

        private bool CreateAssetBundlePromiseIfRequired(IEmote component, in GetSceneEmoteFromRealmIntention intention, IPartitionComponent partitionComponent)
        {
            if (component.AssetResults[intention.BodyShape] != null) return false;

            SceneAssetBundleManifest? manifest = !EnumUtils.HasFlag(intention.PermittedSources, AssetSource.WEB) ? null : component.ManifestResult?.Asset;

            var promise = AssetBundlePromise.Create(World,
                GetAssetBundleIntention.FromHash(typeof(GameObject),
                    intention.Hash + PlatformUtils.GetPlatform(),
                    permittedSources: intention.PermittedSources,
                    customEmbeddedSubDirectory: customStreamingSubdirectory,
                    manifest: manifest,
                    cancellationTokenSource: intention.CancellationTokenSource),
                partitionComponent);

            TryCreateAudioClipPromise(component, intention.BodyShape, partitionComponent);

            component.IsLoading = true;
            World.Create(promise, component, intention.BodyShape);

            return true;
        }

        private void TryCreateAudioClipPromise(IEmote component, BodyShape bodyShape, IPartitionComponent partitionComponent)
        {
            AvatarAttachmentDTO.Content[]? content = component.Model.Asset!.content;

            if (content == null) return;

            foreach (AvatarAttachmentDTO.Content item in content)
            {
                var audioType = item.file.ToAudioType();

                if (audioType == AudioType.UNKNOWN)
                    continue;

                urlBuilder.Clear();
                urlBuilder.AppendDomain(realmData.Ipfs.ContentBaseUrl).AppendPath(new URLPath(item.hash));
                URLAddress url = urlBuilder.Build();

                AudioPromise promise = AudioUtils.CreateAudioClipPromise(World, url.Value, audioType, partitionComponent);
                World.Create(promise, component, bodyShape);
            }
        }

        private static URN GetUrn(string hash, bool loop) =>
            new ($"{SCENE_EMOTE_PREFIX}:{hash}-{loop.ToString().ToLower()}");
    }
}
