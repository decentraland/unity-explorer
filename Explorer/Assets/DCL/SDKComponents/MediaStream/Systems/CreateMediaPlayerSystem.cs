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
using DCL.Settings;
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
        private readonly WorldVolumeMacBus worldVolumeMacBus;
        private readonly MediaPlayerCustomPool mediaPlayerPool;
        private readonly IWebRequestController webRequestController;
        private readonly ObjectProxy<IRoomHub> roomHub;
        private readonly ISceneData sceneData;

        private float worldVolumePercentage = 1f;
        private float masterVolumePercentage = 1f;

        public CreateMediaPlayerSystem(
            World world,
            IWebRequestController webRequestController,
            ObjectProxy<IRoomHub> roomHub,
            ISceneData sceneData,
            MediaPlayerCustomPool mediaPlayerPool,
            ISceneStateProvider sceneStateProvider,
            IPerformanceBudget frameTimeBudget,
            WorldVolumeMacBus worldVolumeMacBus
        ) : base(world)
        {
            this.webRequestController = webRequestController;
            this.roomHub = roomHub;
            this.sceneData = sceneData;
            this.sceneStateProvider = sceneStateProvider;
            this.frameTimeBudget = frameTimeBudget;
            this.worldVolumeMacBus = worldVolumeMacBus;
            this.mediaPlayerPool = mediaPlayerPool;

            //This following part is a workaround applied for the MacOS platform, the reason
            //is related to the video and audio streams, the MacOS environment does not support
            //the volume control for the video and audio streams, as it doesn’t allow to route audio
            //from HLS through to Unity. This is a limitation of Apple’s AVFoundation framework
            //Similar issue reported here https://github.com/RenderHeads/UnityPlugin-AVProVideo/issues/1086
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            this.worldVolumeMacBus = worldVolumeMacBus;
            this.worldVolumeMacBus.OnWorldVolumeChanged += OnWorldVolumeChanged;
            this.worldVolumeMacBus.OnMasterVolumeChanged += OnMasterVolumeChanged;
            masterVolumePercentage = worldVolumeMacBus.GetMasterVolume();
            worldVolumePercentage = worldVolumeMacBus.GetWorldVolume();
#endif
        }

        private void OnWorldVolumeChanged(float volume) =>
            worldVolumePercentage = volume;

        private void OnMasterVolumeChanged(float volume) =>
            masterVolumePercentage = volume;

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
        private void CreateVideoPlayer(in Entity entity, PBVideoPlayer sdkComponent, ref VideoTextureConsumer videoTextureConsumer, [Data] float dt)
        {
            CreateMediaPlayer(dt, entity, sdkComponent.Src, sdkComponent.HasVolume, sdkComponent.Volume);
            videoTextureConsumer.IsDirty = true;
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

            float targetVolume = (hasVolume ? volume : MediaPlayerComponent.DEFAULT_VOLUME) * worldVolumePercentage * masterVolumePercentage;
            component.MediaPlayer.UpdateVolume(sceneStateProvider.IsCurrent ? targetVolume : 0f);

            return component;
        }
    }
}
