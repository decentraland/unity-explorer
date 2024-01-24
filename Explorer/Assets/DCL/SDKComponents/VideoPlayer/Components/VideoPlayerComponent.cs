using DCL.ECSComponents;
using DCL.Optimization.Pools;
using RenderHeads.Media.AVProVideo;
using System;

namespace DCL.SDKComponents.VideoPlayer
{
    public readonly struct VideoPlayerComponent: IPoolableComponentProvider<MediaPlayer>
    {
        public const float DEFAULT_VOLUME = 1f;

        public readonly MediaPlayer MediaPlayer;

        MediaPlayer IPoolableComponentProvider<MediaPlayer>.PoolableComponent => MediaPlayer;
        Type IPoolableComponentProvider<MediaPlayer>.PoolableComponentType => typeof(MediaPlayer);

        public VideoPlayerComponent(PBVideoPlayer sdkComponent, MediaPlayer mediaPlayer)
        {
            this.MediaPlayer = mediaPlayer;

            mediaPlayer.OpenMedia(MediaPathType.AbsolutePathOrURL, sdkComponent.Src, autoPlay: false);

            if (sdkComponent is { HasPlaying: true, Playing: true })
                mediaPlayer.Play();
        }

        public void Dispose()
        {
            MediaPlayer.CloseMedia();
        }
    }
}
