using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.Utilities.Extensions;
using RenderHeads.Media.AVProVideo;
using System;

namespace DCL.SDKComponents.AudioStream
{
    public struct AudioStreamComponent : IPoolableComponentProvider<MediaPlayer>
    {
        public const float DEFAULT_VOLUME = 1f;

        public string URL;

        public MediaPlayer MediaPlayer { get; }

        MediaPlayer IPoolableComponentProvider<MediaPlayer>.PoolableComponent => MediaPlayer;

        Type IPoolableComponentProvider<MediaPlayer>.PoolableComponentType => typeof(MediaPlayer);

        public AudioStreamComponent(PBAudioStream sdkComponent, MediaPlayer mediaPlayer)
        {
            URL = sdkComponent.Url;
            this.MediaPlayer = mediaPlayer;

            if (sdkComponent.Url.IsValidUrl())
            {
                mediaPlayer.OpenMedia(MediaPathType.AbsolutePathOrURL, sdkComponent.Url, autoPlay: false);

                if (sdkComponent is { HasPlaying: true, Playing: true })
                    mediaPlayer.Play();
            }
        }

        public void Dispose()
        {
            MediaPlayer.CloseCurrentStream();
        }
    }
}
