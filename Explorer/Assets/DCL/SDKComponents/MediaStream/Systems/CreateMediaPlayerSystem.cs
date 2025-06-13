using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Connections.Rooms;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using DCL.WebRequests;
using ECS.Abstract;
using ECS.Unity.Groups;
using ECS.Unity.Textures.Components;
#if UNITY_EDITOR
using RenderHeads.Media.AVProVideo;
#endif
using SceneRunner.Scene;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using UnityEngine;

namespace DCL.SDKComponents.MediaStream
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.MEDIA_STREAM)]
    [ThrottlingEnabled]
    public partial class CreateMediaPlayerSystem : BaseUnityLoopSystem
    {
        private static string CONTENT_SERVER_PREFIX = "/content/contents";

        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IPerformanceBudget frameTimeBudget;
        private readonly MediaPlayerCustomPool mediaPlayerPool;
        private readonly IWebRequestController webRequestController;
        private readonly ObjectProxy<IRoomHub> roomHub;
        private readonly ISceneData sceneData;

        public CreateMediaPlayerSystem(
            World world,
            IWebRequestController webRequestController,
            ObjectProxy<IRoomHub> roomHub,
            ISceneData sceneData,
            MediaPlayerCustomPool mediaPlayerPool,
            ISceneStateProvider sceneStateProvider,
            IPerformanceBudget frameTimeBudget
        ) : base(world)
        {
            this.webRequestController = webRequestController;
            this.roomHub = roomHub;
            this.sceneData = sceneData;
            this.sceneStateProvider = sceneStateProvider;
            this.frameTimeBudget = frameTimeBudget;
            this.mediaPlayerPool = mediaPlayerPool;
        }

        protected override void Update(float t)
        {
            CreateAudioStreamQuery(World, t);
            CreateVideoPlayerQuery(World, t);
        }

        [Query]
        [None(typeof(MediaPlayerComponent))]
        private void CreateAudioStream(in Entity entity, ref PBAudioStream sdkComponent, [Data] float dt)
        {
            CreateMediaPlayer(dt, entity, sdkComponent.Url, sdkComponent.HasVolume, sdkComponent.Volume);
        }

        [Query]
        [None(typeof(MediaPlayerComponent))]
        [All(typeof(VideoTextureConsumer))]
        private void CreateVideoPlayer(in Entity entity, PBVideoPlayer sdkComponent, [Data] float dt)
        {
            CreateMediaPlayer(dt, entity, sdkComponent.Src, sdkComponent.HasVolume, sdkComponent.Volume);
        }

        private void CreateMediaPlayer(float dt, Entity entity, string url, bool hasVolume, float volume)
        {
            if (!frameTimeBudget.TrySpendBudget()) return;

            MediaPlayerComponent component = CreateMediaPlayerComponent(entity, url, hasVolume, volume);

            if (component.State != VideoState.VsError)
                component.OpenMediaPromise.UrlReachabilityResolveAsync(webRequestController, component.MediaAddress, GetReportData(), component.Cts.Token).SuppressCancellationThrow().Forget();

            // There is no way to set this from the scene code, at the moment
            // If the player has no transform, it will appear at 0,0,0 and nobody will hear it if it is in 3D
            if (component.MediaPlayer.TryGetAvProPlayer(out var player) && player!.TryGetComponent(out AudioSource mediaPlayerAudio))
                // At the moment we consider streams as global audio always, until there is a way to change it from the scene
                mediaPlayerAudio!.spatialBlend = 0.0f;

            World.Add(entity, component);
        }

        [SuppressMessage("ReSharper", "RedundantAssignment")]
        private MediaPlayerComponent CreateMediaPlayerComponent(Entity entity, string url, bool hasVolume, float volume)
        {
            var isValidLocalPath = false;
            var isValidStreamUrl = false;

            if (url.IsLivekitAddress())
            {
                isValidLocalPath = true;
                isValidStreamUrl = true;
            }

            else

                // if it is not valid, we try get it as a scene local video
            {
                isValidStreamUrl = url.IsValidUrl();

                if (!isValidStreamUrl)
                {
                    isValidLocalPath = sceneData.TryGetMediaUrl(url, out URLAddress mediaUrl);

                    if (isValidLocalPath)
                        url = mediaUrl;
                }
            }

            var address = MediaAddress.New(url);

            MultiMediaPlayer player = address.Match(
                (room: roomHub.StrictObject, mediaPlayerPool),
                onUrlMediaAddress: static (ctx, address) => MultiMediaPlayer.FromAvProPlayer(new AvProPlayer(ctx.mediaPlayerPool.GetOrCreateReusableMediaPlayer(address.Url), ctx.mediaPlayerPool)),
                onLivekitAddress: static (ctx, _) => MultiMediaPlayer.FromLivekitPlayer(new LivekitPlayer(ctx.room))
            );

            var component = new MediaPlayerComponent
            {
                MediaPlayer = player,
                MediaAddress = address,
                IsFromContentServer = url.Contains(CONTENT_SERVER_PREFIX),
                PreviousCurrentTimeChecked = -1,
                LastPropagatedState = VideoState.VsPaused,
                LastPropagatedVideoTime = 0,
                Cts = new CancellationTokenSource(),
                OpenMediaPromise = new OpenMediaPromise(),
            };

            component.SetState(isValidStreamUrl || isValidLocalPath || string.IsNullOrEmpty(url) ? VideoState.VsNone : VideoState.VsError);

#if UNITY_EDITOR
            if (component.MediaPlayer.TryGetAvProPlayer(out var avPro))
                avPro!.gameObject.name = $"MediaPlayer_Entity_{entity}";
#endif

            component.MediaPlayer.UpdateVolume(sceneStateProvider.IsCurrent, hasVolume, volume);

            return component;
        }
    }
}
