using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.Utilities.Extensions;
using RenderHeads.Media.AVProVideo;
using System;
using System.Runtime.CompilerServices;

namespace ECS.Unity.AudioStreams.Components
{
    public struct AudioStreamComponent : IPoolableComponentProvider<MediaPlayer>
    {
        private const float DEFAULT_VOLUME = 1f;
        private static IComponentPool<MediaPlayer> mediaPlayerPool;

        private string url;

        private MediaPlayer mediaPlayer { get; }

        MediaPlayer IPoolableComponentProvider<MediaPlayer>.PoolableComponent => mediaPlayer;

        Type IPoolableComponentProvider<MediaPlayer>.PoolableComponentType => typeof(MediaPlayer);

        public AudioStreamComponent(PBAudioStream sdkComponent, IComponentPoolsRegistry poolsRegistry, bool isCurrentScene)
        {
            mediaPlayerPool ??= poolsRegistry.GetReferenceTypePool<MediaPlayer>();
            mediaPlayer = mediaPlayerPool.Get();

            url = sdkComponent.Url;
            mediaPlayer = mediaPlayer;

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
            mediaPlayerPool = null;
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
        private void UpdateStreamUrl(string url)
        {
            if (url == this.url) return;

            this.url = url;
            mediaPlayer.CloseCurrentStream();
            mediaPlayer.OpenMedia(MediaPathType.AbsolutePathOrURL, url, autoPlay: false);
        }
    }
}
