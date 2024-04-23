using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using CommunicationData.URLHelpers;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.Utilities.Extensions;
using ECS.Abstract;
using ECS.Unity.Groups;
using ECS.Unity.Textures.Components;
using RenderHeads.Media.AVProVideo;
using SceneRunner.Scene;
using UnityEngine;

namespace DCL.SDKComponents.MediaStream
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.MEDIA_STREAM)]
    [ThrottlingEnabled]
    public partial class CreateMediaPlayerSystem : BaseUnityLoopSystem
    {
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IPerformanceBudget frameTimeBudget;
        private readonly IComponentPool<MediaPlayer> mediaPlayerPool;
        private readonly ISceneData sceneData;

        public CreateMediaPlayerSystem(World world, ISceneData sceneData, IComponentPool<MediaPlayer> mediaPlayerPool, ISceneStateProvider sceneStateProvider, IPerformanceBudget frameTimeBudget) : base(world)
        {
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
            if (!frameTimeBudget.TrySpendBudget()) return;

            MediaPlayerComponent component = CreateMediaPlayerComponent(sdkComponent.Url, sdkComponent.HasVolume, sdkComponent.Volume, autoPlay: sdkComponent.HasPlaying && sdkComponent.Playing);
            World.Add(entity, component);
        }

        [Query]
        [None(typeof(MediaPlayerComponent))]
        [All(typeof(VideoTextureComponent))]
        private void CreateVideoPlayer(in Entity entity, ref PBVideoPlayer sdkComponent)
        {
            if (!frameTimeBudget.TrySpendBudget()) return;

            MediaPlayerComponent component = CreateMediaPlayerComponent(sdkComponent.Src, sdkComponent.HasVolume, sdkComponent.Volume, autoPlay: sdkComponent.HasPlaying && sdkComponent.Playing);

            if (component.State != VideoState.VsError)
                component.MediaPlayer.SetPlaybackProperties(sdkComponent);

            World.Add(entity, component);
        }

        private MediaPlayerComponent CreateMediaPlayerComponent(string url, bool hasVolume, float volume, bool autoPlay)
        {
            // if it is not valid, we try get it as a scene local video
            if (!url.IsValidUrl())
            {
                sceneData.TryGetMediaUrl(url, out URLAddress mediaUrl);
                url = mediaUrl;
            }

            var component = new MediaPlayerComponent
            {
                MediaPlayer = mediaPlayerPool.Get(),
                URL = url,
                State = url.IsValidUrl() ? VideoState.VsNone : VideoState.VsError,
            };

            component.MediaPlayer
                     .OpenMediaIfValid(component.URL, autoPlay)
                     .UpdateVolume(sceneStateProvider.IsCurrent, hasVolume, volume);

            return component;
        }
    }
}
