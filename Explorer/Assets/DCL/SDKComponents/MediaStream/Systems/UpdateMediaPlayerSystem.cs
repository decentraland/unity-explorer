using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Utilities.Extensions;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.Unity.Textures.Components;
using ECS.Unity.Transforms.Components;
using RenderHeads.Media.AVProVideo;
using SceneRunner.Scene;
using UnityEngine;
using UnityEngine.Profiling;

namespace DCL.SDKComponents.MediaStream
{
    [UpdateInGroup(typeof(SyncedPresentationSystemGroup))]
    [LogCategory(ReportCategory.MEDIA_STREAM)]
    [ThrottlingEnabled]
    public partial class UpdateMediaPlayerSystem : BaseUnityLoopSystem, ISceneIsCurrentListener
    {
        private readonly ISceneData sceneData;
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IPerformanceBudget frameTimeBudget;
        private readonly MediaFactory mediaFactory;
        private readonly float audioFadeSpeed;
        private readonly Material flipMaterial;

        public UpdateMediaPlayerSystem(
            World world,
            ISceneData sceneData,
            ISceneStateProvider sceneStateProvider,
            IPerformanceBudget frameTimeBudget,
            MediaFactory mediaFactory,
            float audioFadeSpeed,
            Material flipMaterial
        ) : base(world)
        {
            this.sceneData = sceneData;
            this.sceneStateProvider = sceneStateProvider;
            this.frameTimeBudget = frameTimeBudget;
            this.mediaFactory = mediaFactory;
            this.audioFadeSpeed = audioFadeSpeed;
            this.flipMaterial = flipMaterial;
        }

        protected override void Update(float t)
        {
            UpdateMediaPlayerPositionQuery(World);
            UpdateAudioStreamQuery(World, t);
            UpdateVideoStreamQuery(World, t);
            UpdateCustomStreamQuery(World, t);
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
        private void UpdateAudioStream(in Entity entity, ref MediaPlayerComponent component, PBAudioStream sdkComponent, [Data] float dt)
        {
            if (!frameTimeBudget.TrySpendBudget()) return;

            var address = MediaAddress.New(sdkComponent.Url!);

            if (TryReInitializeOnSourceChange(entity, ref component, address)) return;

            component.UpdateState();

            FadeVolume(ref component, sdkComponent.HasVolume ? sdkComponent.Volume : MediaPlayerComponent.DEFAULT_VOLUME, dt);

            if (component.State != VideoState.VsError)
            {
                if (sdkComponent.HasPlaying && sdkComponent.Playing != component.IsPlaying)
                    component.MediaPlayer.UpdatePlayback(sdkComponent.HasPlaying, sdkComponent.Playing, component.MediaPlayer.ActiveInHierarchy);

                if (component.IsPlaying)
                    if (component.MediaPlayer.IsLivekitPlayer(out LivekitPlayer? livekitPlayer))
                        livekitPlayer?.EnsureAudioIsPlaying();
            }

            ConsumePromise(ref component, sdkComponent.HasPlaying && sdkComponent.Playing);

            // Need to re-update state in case an update was needed from the sdk component or promise
            component.UpdateState();
        }

        [Query]
        private void UpdateVideoStream(in Entity entity, ref MediaPlayerComponent component, PBVideoPlayer sdkComponent, [Data] float dt)
        {
            if (!frameTimeBudget.TrySpendBudget()) return;

            var address = MediaAddress.New(sdkComponent.Src!);

            if (TryReInitializeOnSourceChange(entity, ref component, address)) return;

            component.UpdateState();

            FadeVolume(ref component, sdkComponent.HasVolume ? sdkComponent.Volume : MediaPlayerComponent.DEFAULT_VOLUME, dt);

            if (component.State != VideoState.VsError)
            {
                if (sdkComponent.HasPlaying && sdkComponent.Playing != component.IsPlaying)
                {
                    component.MediaPlayer.UpdatePlayback(sdkComponent.HasPlaying, sdkComponent.Playing, component.MediaPlayer.ActiveInHierarchy);
                    component.MediaPlayer.UpdatePlaybackProperties(sdkComponent);
                }

                if (component.IsPlaying)
                    // Covers cases like leaving and re-entering the scene
                    // or the stream not being available for some time, like OBS not started while the stream is active
                    if (component.MediaPlayer.IsLivekitPlayer(out LivekitPlayer? livekitPlayer))
                        livekitPlayer?.EnsureVideoIsPlaying();
            }

            if (ConsumePromise(ref component, false))
                component.MediaPlayer.SetPlaybackProperties(sdkComponent);

            // Need to re-update state in case an update was needed from the sdk component or promise
            component.UpdateState();
        }

        /// <summary>
        ///     If there is no SDK component which controls the playback state, the video is looped and started automatically
        /// </summary>
        [Query]
        private void UpdateCustomStream(ref MediaPlayerComponent mediaPlayer, CustomMediaStream customMediaStream, [Data] float dt)
        {
            if (!frameTimeBudget.TrySpendBudget()) return;

            FadeVolume(ref mediaPlayer, customMediaStream.Volume, dt);

            if (ConsumePromise(ref mediaPlayer, true))
                mediaPlayer.MediaPlayer.SetPlaybackProperties(customMediaStream);
        }

        // This query for all media players regardless of their origin
        [Query]
        private void UpdateVideoTexture(ref MediaPlayerComponent playerComponent, ref VideoTextureConsumer assignedTexture)
        {
            if (!playerComponent.IsPlaying)
            {
                if (playerComponent.State == VideoState.VsError)
                {
                    RenderBlackTexture(ref assignedTexture);
                    return;
                }

                if (playerComponent.State == VideoState.VsPaused)
                    return;
            }

            if (playerComponent.MediaPlayer.IsLivekitPlayer(out LivekitPlayer? livekitPlayer))
            {
                if (!livekitPlayer?.IsVideoOpened ?? false)
                {
                    RenderBlackTexture(ref assignedTexture);
                    return;
                }
            }

            // Video is already playing in the background, and CopyTexture is a GPU operation,
            // so it does not make sense to budget by CPU as it can lead to much worse UX

            Texture? avText = playerComponent.MediaPlayer.LastTexture();
            if (avText == null) return;

            if (!assignedTexture.Texture.HasEqualResolution(to: avText))
                assignedTexture.Resize(avText.width, avText.height);

            if (playerComponent.MediaPlayer.GetTexureScale.Equals(new Vector2(1, -1)))
            {
                //Regular blit or blit with material are called based on the source color space, we blit with material only
                //in case we are in linear and need to convert the colorspace as well as target assigned texture is always in gamma
                if (avText.isDataSRGB)
                    Graphics.Blit(avText, assignedTexture.Texture, new Vector2(1, -1), new Vector2(0, 1));
                else
                    Graphics.Blit(avText, assignedTexture.Texture, flipMaterial);
            }
            else
                Graphics.CopyTexture(avText, assignedTexture.Texture);

            return;

            void RenderBlackTexture(ref VideoTextureConsumer assignedTexture) =>
                Graphics.Blit(Texture2D.blackTexture, assignedTexture.Texture);
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

        private bool TryReInitializeOnSourceChange(in Entity entity, ref MediaPlayerComponent component, MediaAddress address)
        {
            if (component.MediaAddress.IsUrlMediaAddress(out var urlMediaAddress) && address.IsUrlMediaAddress(out var other))
            {
                string selfUrl = urlMediaAddress!.Value.Url;
                string otherUrl = other!.Value.Url;

                if (selfUrl == otherUrl
                    || (sceneData.TryGetMediaUrl(otherUrl, out var localMediaUrl) && selfUrl == localMediaUrl)) return false;

                RemoveAndForceReInitialization(ref component, entity);
                return true;
            }

            if (component.MediaAddress == address) return false;

            RemoveAndForceReInitialization(ref component, entity);
            return true;

            void RemoveAndForceReInitialization(ref MediaPlayerComponent component, Entity entity)
            {
                component.Dispose();
                World.Remove<MediaPlayerComponent>(entity);
            }
        }

        private static bool ConsumePromise(ref MediaPlayerComponent component, bool autoPlay)
        {
            if (!component.OpenMediaPromise.IsResolved) return false;
            if (component.OpenMediaPromise.IsConsumed) return false;
            if (!component.MediaPlayer.ActiveInHierarchy) return false;

            if (component.OpenMediaPromise.IsReachableConsume(component.MediaAddress))
            {
                Profiler.BeginSample(component.MediaPlayer.HasControl
                    ? "MediaPlayer.OpenMedia"
                    : "MediaPlayer.InitialiseAndOpenMedia");

                try { component.MediaPlayer.OpenMedia(component.MediaAddress, component.IsFromContentServer, autoPlay); }
                finally { Profiler.EndSample(); }

                return true;
            }

            component.MarkAsFailed(!component.MediaAddress.IsEmpty);

            Profiler.BeginSample("MediaPlayer.CloseCurrentStream");

            try { component.MediaPlayer.CloseCurrentStream(); }
            finally { Profiler.EndSample(); }

            return false;
        }

        private void FadeVolume(ref MediaPlayerComponent component, float volume, float dt)
        {
            if (component.State != VideoState.VsError)
            {
                float targetVolume = volume * mediaFactory.worldVolumePercentage * mediaFactory.masterVolumePercentage;

                if (!sceneStateProvider.IsCurrent)
                    targetVolume = 0f;

                component.MediaPlayer.CrossfadeVolume(targetVolume, dt * audioFadeSpeed);
            }
        }
    }
}
