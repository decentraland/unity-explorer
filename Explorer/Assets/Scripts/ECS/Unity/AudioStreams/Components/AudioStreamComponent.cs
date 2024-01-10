using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.Utilities.Extensions;
using RenderHeads.Media.AVProVideo;
using System;

namespace ECS.Unity.AudioStreams.Components
{
    public struct AudioStreamComponent : IPoolableComponentProvider<MediaPlayer>
    {
        private string url;

        public MediaPlayer PoolableComponent { get; }

        MediaPlayer IPoolableComponentProvider<MediaPlayer>.PoolableComponent => PoolableComponent;

        Type IPoolableComponentProvider<MediaPlayer>.PoolableComponentType => typeof(MediaPlayer);

        public AudioStreamComponent(PBAudioStream sdkComponent, MediaPlayer mediaPlayer)
        {
            url = sdkComponent.Url;
            PoolableComponent = mediaPlayer;

            if (sdkComponent.HasVolume)
                PoolableComponent.AudioVolume = sdkComponent.Volume;

            if (sdkComponent.Url.IsValidUrl())
            {
                PoolableComponent.OpenMedia(MediaPathType.AbsolutePathOrURL, sdkComponent.Url, autoPlay: false);

                if (sdkComponent is { HasPlaying: true, Playing: true })
                    PoolableComponent.Play();
            }
        }

        public void Dispose()
        {
            PoolableComponent.Stop();
            PoolableComponent.CloseMedia();
            PoolableComponent.Events.RemoveAllListeners();
        }

        public void Update(PBAudioStream sdkComponent)
        {
            if (sdkComponent.Url != url)
                ChangeStreamUrl(sdkComponent.Url);

            if (sdkComponent.HasPlaying && sdkComponent.Playing != PoolableComponent.Control.IsPlaying())
            {
                if (sdkComponent.Playing)
                    PoolableComponent.Play();
                else
                    PoolableComponent.Stop();
            }

            if (sdkComponent.HasVolume)
                PoolableComponent.AudioVolume = sdkComponent.Volume;
        }

        private void ChangeStreamUrl(string url)
        {
            this.url = url;

            if (PoolableComponent.Control.IsPlaying())
                PoolableComponent.Stop();

            PoolableComponent.CloseMedia();
            PoolableComponent.Events.RemoveAllListeners();

            PoolableComponent.OpenMedia(MediaPathType.AbsolutePathOrURL, url, autoPlay: false);
        }
    }
}
