using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Utilities.Extensions;
using DCL.WebRequests;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.Unity.Textures.Components;
using ECS.Unity.Transforms.Components;
using SceneRunner.Scene;
using UnityEngine;
using UnityEngine.Profiling;
using Utility;

namespace DCL.SDKComponents.MediaStream
{
    [UpdateInGroup(typeof(SyncedPresentationSystemGroup))]
    [LogCategory(ReportCategory.MEDIA_STREAM)]
    [ThrottlingEnabled]
    public partial class UpdateMediaPlayerSystem : BaseUnityLoopSystem, ISceneIsCurrentListener
    {
        private readonly IWebRequestController webRequestController;
        private readonly ISceneData sceneData;
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IPerformanceBudget frameTimeBudget;
        private readonly VolumeBus volumeBus;

        private readonly float audioFadeSpeed;

        private float worldVolumePercentage = 1f;
        private float masterVolumePercentage = 1f;

        public UpdateMediaPlayerSystem(
            World world,
            IWebRequestController webRequestController,
            ISceneData sceneData,
            ISceneStateProvider sceneStateProvider,
            IPerformanceBudget frameTimeBudget,
            VolumeBus volumeBus,
            float audioFadeSpeed
        ) : base(world)
        {
            this.webRequestController = webRequestController;
            this.sceneData = sceneData;
            this.sceneStateProvider = sceneStateProvider;
            this.frameTimeBudget = frameTimeBudget;
            this.audioFadeSpeed = audioFadeSpeed;

            //This following part is a workaround applied for the MacOS platform, the reason
            //is related to the video and audio streams, the MacOS environment does not support
            //the volume control for the video and audio streams, as it doesn’t allow to route audio
            //from HLS through to Unity. This is a limitation of Apple’s AVFoundation framework
            //Similar issue reported here https://github.com/RenderHeads/UnityPlugin-AVProVideo/issues/1086
            this.volumeBus = volumeBus;
            this.volumeBus.OnWorldVolumeChanged += OnWorldVolumeChanged;
            this.volumeBus.OnMasterVolumeChanged += OnMasterVolumeChanged;
            masterVolumePercentage = volumeBus.GetSerializedMasterVolume();
            worldVolumePercentage = volumeBus.GetSerializedWorldVolume();
        }

        protected override void OnDispose()
        {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            volumeBus.OnWorldVolumeChanged -= OnWorldVolumeChanged;
            volumeBus.OnMasterVolumeChanged -= OnMasterVolumeChanged;
#endif
        }

        protected override void Update(float t)
        {
            UpdateMediaPlayerPositionQuery(World);
            UpdateAudioStreamQuery(World, t);
            UpdateVideoStreamQuery(World, t);
            UpdateVideoTextureQuery(World);
        }

        public void OnSceneIsCurrentChanged(bool enteredScene)
        {
            ToggleCurrentStreamsStateQuery(World, enteredScene);
        }

        [Query]
        private void UpdateMediaPlayerPosition(ref MediaPlayerComponent mediaPlayer, ref TransformComponent transformComponent)
        {
            mediaPlayer.MediaPlayer.PlaceAt(transformComponent.Transform.position);
        }

        [Query]
        private void UpdateAudioStream(ref MediaPlayerComponent component, PBAudioStream sdkComponent, [Data] float dt)
        {
            if (!frameTimeBudget.TrySpendBudget()) return;

            var address = MediaAddress.New(sdkComponent.Url!);

            // In case of source updating we wait until the next update
            if (TryUpdateSource(ref component, address)) return;

            if (component.State != VideoState.VsError)
            {
                if (sdkComponent.HasPlaying && sdkComponent.Playing != component.IsPlaying)
                    component.MediaPlayer.UpdatePlayback(sdkComponent.HasPlaying, sdkComponent.Playing);

                float targetVolume = (sdkComponent.HasVolume ? sdkComponent.Volume : MediaPlayerComponent.DEFAULT_VOLUME) * worldVolumePercentage * masterVolumePercentage;

                if (!sceneStateProvider.IsCurrent)
                    targetVolume = 0f;

                component.MediaPlayer.CrossfadeVolume(targetVolume, dt * audioFadeSpeed);
            }

            ConsumePromise(ref component, sdkComponent.HasPlaying && sdkComponent.Playing);
        }

        [Query]
        private void UpdateVideoStream(ref MediaPlayerComponent component, PBVideoPlayer sdkComponent, [Data] float dt)
        {
            if (!frameTimeBudget.TrySpendBudget()) return;

            var address = MediaAddress.New(sdkComponent.Src!);

            if (TryUpdateSource(ref component, address)) return;

            if (component.State != VideoState.VsError)
            {
                if (sdkComponent.HasPlaying && sdkComponent.Playing != component.IsPlaying)
                {
                    component.MediaPlayer.UpdatePlayback(sdkComponent.HasPlaying, sdkComponent.Playing);
                    component.MediaPlayer.UpdatePlaybackProperties(sdkComponent);
                }

                float targetVolume = (sdkComponent.HasVolume ? sdkComponent.Volume : MediaPlayerComponent.DEFAULT_VOLUME) * worldVolumePercentage * masterVolumePercentage;

                if (!sceneStateProvider.IsCurrent)
                    targetVolume = 0f;

                component.MediaPlayer.CrossfadeVolume(targetVolume, dt * audioFadeSpeed);
            }

            if (ConsumePromise(ref component, false))
                component.MediaPlayer.SetPlaybackProperties(sdkComponent);
        }

        [Query]
        [All(typeof(PBVideoPlayer))]
        private void UpdateVideoTexture(ref MediaPlayerComponent playerComponent, ref VideoTextureConsumer assignedTexture)
        {
            playerComponent.MediaPlayer.EnsurePlaying();

            if (!playerComponent.IsPlaying
                || playerComponent.State == VideoState.VsError
                || !playerComponent.MediaPlayer.MediaOpened
               )
                return;

            // Video is already playing in the background, and CopyTexture is a GPU operation,
            // so it does not make sense to budget by CPU as it can lead to much worse UX

            Texture? avText = playerComponent.MediaPlayer.LastTexture();
            if (avText == null) return;

            // Handle texture update
            if (assignedTexture.Texture.Asset.HasEqualResolution(to: avText))
                Graphics.CopyTexture(avText, assignedTexture.Texture);
            else
                assignedTexture.Texture.Asset.ResizeTexture(to: avText); // will be updated on the next frame/update-loop
        }

        [Query]
        private void ToggleCurrentStreamsState(Entity entity, MediaPlayerComponent mediaPlayerComponent, [Data] bool enteredScene)
        {
            if (mediaPlayerComponent.MediaPlayer.IsLivekitPlayer(out LivekitPlayer livekitPlayer) && !enteredScene)
            {
                //Streams rely on livekit room being active; which can only be in we are on the same scene. Next time we enter the scene, it will be recreate by
                //the regular CreateMediaPlayerSystem
                mediaPlayerComponent.Dispose();
                World.Remove<MediaPlayerComponent>(entity);
            }
        }

        private bool TryUpdateSource(ref MediaPlayerComponent component,
            MediaAddress mediaAddress)
        {
            if (component.MediaAddress != mediaAddress && ShouldUpdateSource(in component))
            {
                component.MediaPlayer.CloseCurrentStream();

                UpdateStreamUrl(ref component, mediaAddress);

                if (component.State != VideoState.VsError)
                {
                    component.Cts = component.Cts.SafeRestart();
                    component.OpenMediaPromise.UrlReachabilityResolveAsync(webRequestController, component.MediaAddress, GetReportData(), component.Cts.Token).Forget();
                }

                return true;
            }

            return false;

            bool ShouldUpdateSource(in MediaPlayerComponent component) =>
                component.MediaAddress.Match(
                    (sceneData, mediaAddress),
                    onUrlMediaAddress: static (ctx, componentAddress) =>
                    {
                        string mediaAddressUrl = ctx.mediaAddress.IsUrlMediaAddress(out var otherUrl) ? otherUrl!.Value.Url : "";
                        return !ctx.sceneData.TryGetMediaUrl(mediaAddressUrl, out URLAddress localMediaUrl) || componentAddress.Url != localMediaUrl;
                    },
                    onLivekitAddress: static (_, _) => true
                );

            void UpdateStreamUrl(ref MediaPlayerComponent component, MediaAddress mediaAddress)
            {
                if (component.MediaAddress.IsLivekitAddress(out _))
                {
                    component.MediaAddress = mediaAddress;
                    return;
                }

                mediaAddress.IsUrlMediaAddress(out var urlMediaAddress);
                string url = urlMediaAddress!.Value.Url;

                bool isValidStreamUrl = url.IsValidUrl();
                bool isValidLocalPath = false;

                if (!isValidStreamUrl)
                {
                    isValidLocalPath = sceneData.TryGetMediaUrl(url, out URLAddress mediaUrl);

                    if (isValidLocalPath)
                        mediaAddress = MediaAddress.New(mediaUrl.Value);
                }

                component.MediaAddress = mediaAddress;
                component.SetState(isValidStreamUrl || isValidLocalPath || mediaAddress.IsEmpty ? VideoState.VsNone : VideoState.VsError);
            }
        }

        private static bool ConsumePromise(ref MediaPlayerComponent component, bool autoPlay)
        {
            if (!component.OpenMediaPromise.IsResolved) return false;
            if (component.OpenMediaPromise.IsConsumed) return false;

            if (component.OpenMediaPromise.IsReachableConsume(component.MediaAddress))
            {
                Profiler.BeginSample(component.MediaPlayer.HasControl
                    ? "MediaPlayer.OpenMedia"
                    : "MediaPlayer.InitialiseAndOpenMedia");

                try { component.MediaPlayer.OpenMedia(component.MediaAddress, component.IsFromContentServer, autoPlay); }
                finally { Profiler.EndSample(); }

                return true;
            }

            component.SetState(component.MediaAddress.IsEmpty ? VideoState.VsNone : VideoState.VsError);
            Profiler.BeginSample("MediaPlayer.CloseCurrentStream");

            try { component.MediaPlayer.CloseCurrentStream(); }
            finally { Profiler.EndSample(); }

            return false;
        }

        private void OnWorldVolumeChanged(float volume)
        {
            worldVolumePercentage = volume;
        }

        private void OnMasterVolumeChanged(float volume)
        {
            masterVolumePercentage = volume;
        }
    }
}
