using DCL.ECSComponents;
using DCL.Optimization.Pools;
using RenderHeads.Media.AVProVideo;
using System;

namespace DCL.SDKComponents.VideoPlayer
{
    public struct VideoPlayerComponent: IPoolableComponentProvider<MediaPlayer>
    {
        private readonly PBVideoPlayer sdkComponent;
        public readonly MediaPlayer mediaPlayer;

        MediaPlayer IPoolableComponentProvider<MediaPlayer>.PoolableComponent => mediaPlayer;
        Type IPoolableComponentProvider<MediaPlayer>.PoolableComponentType => typeof(MediaPlayer);

        public VideoPlayerComponent(PBVideoPlayer sdkComponent, MediaPlayer mediaPlayer)
        {
            this.sdkComponent = sdkComponent;
            this.mediaPlayer = mediaPlayer;

            mediaPlayer.OpenMedia(MediaPathType.AbsolutePathOrURL, sdkComponent.Src, autoPlay: true); // TODO: change auto-play to 'false'

            if (sdkComponent is { HasPlaying: true, Playing: true })
                mediaPlayer.Play();
        }

        public void Dispose()
        {
            mediaPlayer.CloseMedia();
        }
    }
}
