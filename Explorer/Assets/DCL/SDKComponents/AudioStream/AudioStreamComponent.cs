using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.Utilities.Extensions;
using RenderHeads.Media.AVProVideo;
using System;
using System.Runtime.CompilerServices;

namespace DCL.SDKComponents.AudioStream
{
    public struct AudioStreamComponent : IPoolableComponentProvider<MediaPlayer>
    {
        private const float DEFAULT_VOLUME = 1f;
        private string url;

        public MediaPlayer MediaPlayer { get; }

        MediaPlayer IPoolableComponentProvider<MediaPlayer>.PoolableComponent => MediaPlayer;

        Type IPoolableComponentProvider<MediaPlayer>.PoolableComponentType => typeof(MediaPlayer);

        public AudioStreamComponent(PBAudioStream sdkComponent, MediaPlayer mediaPlayer, bool isCurrentScene)
        {
            url = sdkComponent.Url;
            this.MediaPlayer = mediaPlayer;

            UpdateVolume(sdkComponent, isCurrentScene);

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

        public void UpdateComponentChange(PBAudioStream sdkComponent)
        {
            UpdateStreamUrl(sdkComponent.Url);
            UpdatePlayback(sdkComponent);
        }

        public void UpdateVolume(PBAudioStream sdkComponent, bool isCurrentScene)
        {
            if (isCurrentScene)
                MediaPlayer.AudioVolume = sdkComponent.HasVolume ? sdkComponent.Volume : DEFAULT_VOLUME;
            else
                MediaPlayer.AudioVolume = 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdatePlayback(PBAudioStream sdkComponent)
        {
            if (sdkComponent.HasPlaying && sdkComponent.Playing != MediaPlayer.Control.IsPlaying())
            {
                if (sdkComponent.Playing)
                    MediaPlayer.Play();
                else
                    MediaPlayer.Stop();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateStreamUrl(string newUrl)
        {
            if (this.url == newUrl) return;

            this.url = newUrl;
            MediaPlayer.CloseCurrentStream();
            MediaPlayer.OpenMedia(MediaPathType.AbsolutePathOrURL, newUrl, autoPlay: false);
        }
    }
}
