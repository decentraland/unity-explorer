using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
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
        private readonly IComponentPool<MediaPlayer> mediaPlayerPool;
        private readonly IWebRequestController webRequestController;
        private readonly ISceneData sceneData;

        public CreateMediaPlayerSystem(World world, IWebRequestController webRequestController, ISceneData sceneData, IComponentPool<MediaPlayer> mediaPlayerPool, ISceneStateProvider sceneStateProvider,
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
        private void CreateAudioStream(in Entity entity, TransformComponent transformComponent, ref PBAudioStream sdkComponent)
        {
            CreateMediaPlayer(entity, transformComponent.Transform.position, sdkComponent.Url, sdkComponent.HasVolume, sdkComponent.Volume);
        }

        [Query]
        [None(typeof(MediaPlayerComponent))]
        [All(typeof(VideoTextureConsumer))]
        private void CreateVideoPlayer(in Entity entity, TransformComponent transformComponent, PBVideoPlayer sdkComponent)
        {
            CreateMediaPlayer(entity, transformComponent.Transform.position, sdkComponent.Src, sdkComponent.HasVolume, sdkComponent.Volume);
        }

        private void CreateMediaPlayer(Entity entity, Vector3 initialPosition, string url, bool hasVolume, float volume)
        {
            if (!frameTimeBudget.TrySpendBudget()) return;

            MediaPlayerComponent component = CreateMediaPlayerComponent(entity, url, hasVolume, volume);

            if (component.State != VideoState.VsError)
                component.OpenMediaPromise.UrlReachabilityResolveAsync(webRequestController, component.URL, GetReportData(), component.Cts.Token).SuppressCancellationThrow().Forget();

            // Needed for positional sound
            component.MediaPlayer.transform.position = initialPosition;

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

            var component = new MediaPlayerComponent
            {
                MediaPlayer = mediaPlayerPool.Get(),
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
