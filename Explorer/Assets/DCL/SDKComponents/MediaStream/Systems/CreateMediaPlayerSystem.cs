﻿using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Utilities.Extensions;
using DCL.WebRequests;
using ECS.Abstract;
using ECS.Unity.Groups;
using ECS.Unity.Textures.Components;
using ECS.Unity.Transforms.Components;
using RenderHeads.Media.AVProVideo;
using SceneRunner.Scene;
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
        private readonly ISceneData sceneData;

        public CreateMediaPlayerSystem(World world, IWebRequestController webRequestController, ISceneData sceneData, MediaPlayerCustomPool mediaPlayerPool, ISceneStateProvider sceneStateProvider,
            IPerformanceBudget frameTimeBudget) : base(world)
        {
            this.webRequestController = webRequestController;
            this.sceneData = sceneData;
            this.sceneStateProvider = sceneStateProvider;
            this.frameTimeBudget = frameTimeBudget;
            this.mediaPlayerPool = mediaPlayerPool;
        }

        protected override void Update(float t)
        {
            CreateAudioStreamQuery(World);
            CreateVideoPlayerQuery(World);
        }

        [Query]
        [None(typeof(MediaPlayerComponent))]
        private void CreateAudioStream(in Entity entity, ref PBAudioStream sdkComponent)
        {
            CreateMediaPlayer(entity, sdkComponent.Url, sdkComponent.HasVolume, sdkComponent.Volume);
        }

        [Query]
        [None(typeof(MediaPlayerComponent))]
        [All(typeof(VideoTextureConsumer))]
        private void CreateVideoPlayer(in Entity entity, PBVideoPlayer sdkComponent)
        {
            CreateMediaPlayer(entity, sdkComponent.Src, sdkComponent.HasVolume, sdkComponent.Volume);
        }

        private void CreateMediaPlayer(Entity entity, string url, bool hasVolume, float volume)
        {
            if (!frameTimeBudget.TrySpendBudget()) return;

            MediaPlayerComponent component = CreateMediaPlayerComponent(entity, url, hasVolume, volume);

            if (component.State != VideoState.VsError)
                component.OpenMediaPromise.UrlReachabilityResolveAsync(webRequestController, component.URL, GetReportData(), component.Cts.Token).SuppressCancellationThrow().Forget();

            // There is no way to set this from the scene code, at the moment
            // If the player has no transform, it will appear at 0,0,0 and nobody will hear it if it is in 3D
            if (component.MediaPlayer.TryGetComponent(out AudioSource mediaPlayerAudio))
                mediaPlayerAudio.spatialBlend = World.Has<TransformComponent>(entity) ? 1.0f : 0.0f;

            World.Add(entity, component);
        }

        private MediaPlayerComponent CreateMediaPlayerComponent(Entity entity, string url, bool hasVolume, float volume)
        {
            // if it is not valid, we try get it as a scene local video
            bool isValidStreamUrl = url.IsValidUrl();
            bool isValidLocalPath = false;

            if (!isValidStreamUrl)
            {
                isValidLocalPath = sceneData.TryGetMediaUrl(url, out URLAddress mediaUrl);
                if(isValidLocalPath)
                    url = mediaUrl;
            }

            MediaPlayer mediaPlayer = mediaPlayerPool.TryGetReusableMediaPlayer(url);
            var component = new MediaPlayerComponent
            {
                MediaPlayer = mediaPlayer,
                URL = url,
                IsFromContentServer = url.Contains(CONTENT_SERVER_PREFIX),
                PreviousCurrentTimeChecked = -1,
                LastPropagatedState = VideoState.VsPaused,
                LastPropagatedVideoTime = 0,
                Cts = new CancellationTokenSource(),
                OpenMediaPromise = new OpenMediaPromise(),
            };
            component.SetState(isValidStreamUrl || isValidLocalPath || string.IsNullOrEmpty(url) ? VideoState.VsNone : VideoState.VsError);

#if UNITY_EDITOR
            component.MediaPlayer.gameObject.name = $"MediaPlayer_Entity_{entity}";
#endif

            component.MediaPlayer.UpdateVolume(sceneStateProvider.IsCurrent, hasVolume, volume);

            return component;
        }
    }
}
