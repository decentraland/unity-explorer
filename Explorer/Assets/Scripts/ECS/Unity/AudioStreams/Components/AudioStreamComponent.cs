using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.Utilities.Extensions;
using RenderHeads.Media.AVProVideo;
using System;

namespace ECS.Unity.AudioStreams.Components
{
    public struct AudioStreamComponent : IPoolableComponentProvider<MediaPlayer>
    {
        private static IComponentPool<MediaPlayer> mediaPlayerPool;

        private string url;

        private MediaPlayer mediaPlayer { get; }

        MediaPlayer IPoolableComponentProvider<MediaPlayer>.PoolableComponent => mediaPlayer;

        Type IPoolableComponentProvider<MediaPlayer>.PoolableComponentType => typeof(MediaPlayer);

        public AudioStreamComponent(PBAudioStream sdkComponent, IComponentPoolsRegistry poolsRegistry)
        {
            mediaPlayerPool ??= poolsRegistry.GetReferenceTypePool<MediaPlayer>();
            mediaPlayer = mediaPlayerPool.Get();

            url = sdkComponent.Url;
            mediaPlayer = mediaPlayer;

            if (sdkComponent.HasVolume)
                mediaPlayer.AudioVolume = sdkComponent.Volume;

            if (sdkComponent.Url.IsValidUrl())
            {
                mediaPlayer.OpenMedia(MediaPathType.AbsolutePathOrURL, sdkComponent.Url, autoPlay: false);

                if (sdkComponent is { HasPlaying: true, Playing: true })
                    mediaPlayer.Play();
            }
        }

        public void Dispose()
        {
            mediaPlayerPool = null;

            mediaPlayer.Stop();
            mediaPlayer.CloseMedia();
            mediaPlayer.Events.RemoveAllListeners();
        }

        public void Update(PBAudioStream sdkComponent)
        {
            if (sdkComponent.Url != url)
                ChangeStreamUrl(sdkComponent.Url);

            if (sdkComponent.HasPlaying && sdkComponent.Playing != mediaPlayer.Control.IsPlaying())
            {
                if (sdkComponent.Playing)
                    mediaPlayer.Play();
                else
                    mediaPlayer.Stop();
            }

            if (sdkComponent.HasVolume)
                mediaPlayer.AudioVolume = sdkComponent.Volume;
        }

        private void ChangeStreamUrl(string url)
        {
            this.url = url;

            if (mediaPlayer.Control.IsPlaying())
                mediaPlayer.Stop();

            mediaPlayer.CloseMedia();
            mediaPlayer.Events.RemoveAllListeners();

            mediaPlayer.OpenMedia(MediaPathType.AbsolutePathOrURL, url, autoPlay: false);
        }
    }
}
