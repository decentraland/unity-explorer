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

        public bool IsPlaying => MediaPlayer.Control.IsPlaying();

        MediaPlayer IPoolableComponentProvider<MediaPlayer>.PoolableComponent => MediaPlayer;
        Type IPoolableComponentProvider<MediaPlayer>.PoolableComponentType => typeof(MediaPlayer);

        public VideoPlayerComponent(PBVideoPlayer sdkComponent, MediaPlayer mediaPlayer)
        {
            URL = sdkComponent.Src;
            MediaPlayer = mediaPlayer;

            if (sdkComponent.Src.IsValidUrl())
            {
                mediaPlayer.OpenMedia(MediaPathType.AbsolutePathOrURL, sdkComponent.Src, autoPlay: false);

                if (sdkComponent is { HasPlaying: true, Playing: true })
                    mediaPlayer.Play();
            }

            mediaPlayer.Loop = sdkComponent.HasLoop && sdkComponent.Loop; // default: loop = false
            mediaPlayer.Control.SetPlaybackRate(sdkComponent.HasPlaybackRate ? sdkComponent.PlaybackRate : DEFAULT_PLAYBACK_RATE);
            mediaPlayer.Control.Seek(sdkComponent.HasPosition ? sdkComponent.Position : DEFAULT_POSITION);
        }

        public void Dispose()
        {
            MediaPlayer.CloseCurrentStream();
        }
    }
}
