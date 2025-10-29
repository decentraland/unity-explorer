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

        public UpdateMediaPlayerSystem(
            World world,
            ISceneData sceneData,
            ISceneStateProvider sceneStateProvider,
            IPerformanceBudget frameTimeBudget,
            MediaFactory mediaFactory,
            float audioFadeSpeed
        ) : base(world)
        {
            this.sceneData = sceneData;
            this.sceneStateProvider = sceneStateProvider;
            this.frameTimeBudget = frameTimeBudget;
            this.mediaFactory = mediaFactory;
            this.audioFadeSpeed = audioFadeSpeed;
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

            FadeVolume(ref component, sdkComponent.HasVolume ? sdkComponent.Volume : MediaPlayerComponent.DEFAULT_VOLUME, dt);

            if (component.State != VideoState.VsError)
            {
                if (sdkComponent.HasPlaying && sdkComponent.Playing != component.IsPlaying)
                    component.MediaPlayer.UpdatePlayback(sdkComponent.HasPlaying, sdkComponent.Playing);

                if (component.IsPlaying)
                    component.MediaPlayer.EnsurePlaying();
            }

            ConsumePromise(ref component, sdkComponent.HasPlaying && sdkComponent.Playing);
        }

        [Query]
        private void UpdateVideoStream(in Entity entity, ref MediaPlayerComponent component, PBVideoPlayer sdkComponent, [Data] float dt)
        {
            if (!frameTimeBudget.TrySpendBudget()) return;

            var address = MediaAddress.New(sdkComponent.Src!);

            if (TryReInitializeOnSourceChange(entity, ref component, address)) return;

            FadeVolume(ref component, sdkComponent.HasVolume ? sdkComponent.Volume : MediaPlayerComponent.DEFAULT_VOLUME, dt);

            if (component.State != VideoState.VsError)
            {
                if (sdkComponent.HasPlaying && sdkComponent.Playing != component.IsPlaying)
                {
                    component.MediaPlayer.UpdatePlayback(sdkComponent.HasPlaying, sdkComponent.Playing);
                    component.MediaPlayer.UpdatePlaybackProperties(sdkComponent);
                }

                if (component.IsPlaying)
                    component.MediaPlayer.EnsurePlaying();
            }

            if (ConsumePromise(ref component, false))
                component.MediaPlayer.SetPlaybackProperties(sdkComponent);
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
            if (!playerComponent.IsPlaying
                || playerComponent.State == VideoState.VsError
                || !playerComponent.MediaPlayer.MediaOpened
               )
                return;

            // Video is already playing in the background, and CopyTexture is a GPU operation,
            // so it does not make sense to budget by CPU as it can lead to much worse UX

            Texture? avText = playerComponent.MediaPlayer.LastTexture();
            if (avText == null) return;

            if (!assignedTexture.Texture.HasEqualResolution(to: avText))
                assignedTexture.Resize(avText.width, avText.height);

            if (playerComponent.MediaPlayer.GetTexureScale.Equals(new Vector2(1, -1)))
                Graphics.Blit(avText, assignedTexture.Texture, new Vector2(1, -1), new Vector2(0, 1));
            else
                Graphics.CopyTexture(avText, assignedTexture.Texture);
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
