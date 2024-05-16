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
using RenderHeads.Media.AVProVideo;
using SceneRunner.Scene;
using System.Threading;

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
        private void CreateAudioStream(in Entity entity, ref PBAudioStream sdkComponent)
        {
            CreateMediaPlayer(entity, sdkComponent.Url, sdkComponent.HasVolume, sdkComponent.Volume);
        }

        [Query]
        [None(typeof(MediaPlayerComponent))]
        [All(typeof(VideoTextureComponent))]
        private void CreateVideoPlayer(in Entity entity, PBVideoPlayer sdkComponent)
        {
            CreateMediaPlayer(entity, sdkComponent.Src, sdkComponent.HasVolume, sdkComponent.Volume);
        }

        private void CreateMediaPlayer(in Entity entity, string url, bool hasVolume, float volume)
        {
            if (!frameTimeBudget.TrySpendBudget()) return;

            MediaPlayerComponent component = CreateMediaPlayerComponent(url, hasVolume, volume);

            if (component.State != VideoState.VsError)
                component.OpenMediaPromise.UrlReachabilityResolveAsync(webRequestController, component.URL, component.Cts.Token).SuppressCancellationThrow().Forget();

            World.Add(entity, component);
        }

        private MediaPlayerComponent CreateMediaPlayerComponent(string url, bool hasVolume, float volume)
        {
            // if it is not valid, we try get it as a scene local video
            if (!url.IsValidUrl() && sceneData.TryGetMediaUrl(url, out URLAddress mediaUrl))
                url = mediaUrl;

            var component = new MediaPlayerComponent
            {
                MediaPlayer = mediaPlayerPool.Get(),
                URL = url,
                State = url.IsValidUrl() ? VideoState.VsNone : VideoState.VsError,
                Cts = new CancellationTokenSource(),
                OpenMediaPromise = new OpenMediaPromise(),
            };

            component.MediaPlayer.UpdateVolume(sceneStateProvider.IsCurrent, hasVolume, volume);

            return component;
        }
    }
}
