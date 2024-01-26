using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.AudioStream;
using DCL.Utilities.Extensions;
using RenderHeads.Media.AVProVideo;
using System;

namespace DCL.SDKComponents.VideoPlayer
{
    public struct VideoPlayerComponent : IPoolableComponentProvider<MediaPlayer>
    {
        public const float DEFAULT_VOLUME = 1f;
        public const float DEFAULT_PLAYBACK_RATE = 1f;
        public const float DEFAULT_POSITION = 0f;

        public readonly MediaPlayer MediaPlayer;
        public string URL;

        public VideoState CurrentState;

        public bool IsPlaying => MediaPlayer.Control.IsPlaying();

        public float CurrentTime => (float)MediaPlayer.Control.GetCurrentTime();
        public float Duration => (float)MediaPlayer.Info.GetDuration();

        MediaPlayer IPoolableComponentProvider<MediaPlayer>.PoolableComponent => MediaPlayer;
        Type IPoolableComponentProvider<MediaPlayer>.PoolableComponentType => typeof(MediaPlayer);

        public VideoPlayerComponent(PBVideoPlayer sdkComponent, MediaPlayer mediaPlayer)
        {
            URL = sdkComponent.Src;
            MediaPlayer = mediaPlayer;

            CurrentState = VideoState.VsLoading;
            // mediaPlayer.Events.AddListener(OnMediaPlayerEvent);

            if (sdkComponent.Src.IsValidUrl())
            {
                mediaPlayer.OpenMedia(MediaPathType.AbsolutePathOrURL, sdkComponent.Src, autoPlay: false);

                if (sdkComponent is { HasPlaying: true, Playing: true })
                    mediaPlayer.Play();
            }
            else
                CurrentState = VideoState.VsError;

            mediaPlayer.Loop = sdkComponent.HasLoop && sdkComponent.Loop; // default: loop = false
            mediaPlayer.Control.SetPlaybackRate(sdkComponent.HasPlaybackRate ? sdkComponent.PlaybackRate : DEFAULT_PLAYBACK_RATE);
            mediaPlayer.Control.Seek(sdkComponent.HasPosition ? sdkComponent.Position : DEFAULT_POSITION);
        }

        public void Dispose()
        {
            MediaPlayer.CloseCurrentStream();
        }

        public bool StateHasChanged()
        {
            VideoState newState = CheckCurrentState(MediaPlayer);

            if (newState != CurrentState)
            {
                CurrentState = newState;
                return true;
            }

            return false;
        }

        private static VideoState CheckCurrentState(MediaPlayer mediaPlayer)
        {
            if (mediaPlayer.Control.IsPlaying()) return VideoState.VsPlaying;
            if (mediaPlayer.Control.IsPaused()) return VideoState.VsPaused;
            if (mediaPlayer.Control.IsFinished()) return VideoState.VsNone;
            if (mediaPlayer.Control.IsBuffering()) return VideoState.VsBuffering;
            if (mediaPlayer.Control.IsSeeking()) return VideoState.VsSeeking;

            if (mediaPlayer.Control.GetLastError() != ErrorCode.None) return VideoState.VsError;

            return VideoState.VsNone;
        }

        private void OnMediaPlayerEvent(MediaPlayer _, MediaPlayerEvent.EventType eventType, ErrorCode __)
        {
            VideoState newState = eventType switch
                                  {
                                      MediaPlayerEvent.EventType.MetaDataReady => VideoState.VsLoading,
                                      MediaPlayerEvent.EventType.ReadyToPlay => VideoState.VsReady,
                                      MediaPlayerEvent.EventType.Started => VideoState.VsPlaying,
                                      MediaPlayerEvent.EventType.FirstFrameReady => VideoState.VsPlaying,
                                      MediaPlayerEvent.EventType.FinishedPlaying => VideoState.VsNone,
                                      MediaPlayerEvent.EventType.Error => VideoState.VsError,
                                      MediaPlayerEvent.EventType.StartedBuffering => VideoState.VsBuffering,
                                      MediaPlayerEvent.EventType.FinishedBuffering => VideoState.VsPlaying, // You might want to set this to the most likely next state, e.g., PLAYING
                                      MediaPlayerEvent.EventType.StartedSeeking => VideoState.VsSeeking,
                                      MediaPlayerEvent.EventType.FinishedSeeking => // After seeking, the state might return to PLAYING or PAUSED, for example
                                          MediaPlayer.Control.IsPlaying() ? VideoState.VsPlaying : VideoState.VsPaused,
                                      _ => CurrentState,
                                  };

            // if (CurrentState != newState)
            // {
            //     StateHasChanged = true;
            //     CurrentState = newState;
            // }
        }
    }
}
