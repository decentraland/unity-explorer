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
        private readonly MediaPlayer mediaPlayer;
        private string url;

        MediaPlayer IPoolableComponentProvider<MediaPlayer>.PoolableComponent => mediaPlayer;

        Type IPoolableComponentProvider<MediaPlayer>.PoolableComponentType => typeof(MediaPlayer);

        public AudioStreamComponent(PBAudioStream sdkComponent, MediaPlayer mediaPlayer, bool isCurrentScene)
        {
            url = sdkComponent.Url;
            this.mediaPlayer = mediaPlayer;

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
            mediaPlayer.CloseCurrentStream();
        }

        public void UpdateComponentChange(PBAudioStream sdkComponent)
        {
            UpdateStreamUrl(sdkComponent.Url);
            UpdatePlayback(sdkComponent);
        }

        public void UpdateVolume(PBAudioStream sdkComponent, bool isCurrentScene)
        {
            if (isCurrentScene)
                mediaPlayer.AudioVolume = sdkComponent.HasVolume ? sdkComponent.Volume : DEFAULT_VOLUME;
            else
                mediaPlayer.AudioVolume = 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdatePlayback(PBAudioStream sdkComponent)
        {
            if (sdkComponent.HasPlaying && sdkComponent.Playing != mediaPlayer.Control.IsPlaying())
            {
                if (sdkComponent.Playing)
                    mediaPlayer.Play();
                else
                    mediaPlayer.Stop();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateStreamUrl(string newUrl)
        {
            if (this.url == newUrl) return;

            this.url = newUrl;
            mediaPlayer.CloseCurrentStream();
            mediaPlayer.OpenMedia(MediaPathType.AbsolutePathOrURL, newUrl, autoPlay: false);
        }
    }
}
