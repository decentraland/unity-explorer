using DCL.ECSComponents;
using DCL.Optimization.Pools;
using RenderHeads.Media.AVProVideo;
using System;

namespace ECS.Unity.AudioStreams.Components
{
    public readonly struct AudioStreamComponent : IPoolableComponentProvider<MediaPlayer>
    {
        public readonly PBAudioStream PBAudioStream;
        public readonly MediaPlayer MediaPlayer;

        public MediaPlayer PoolableComponent => MediaPlayer;

        MediaPlayer IPoolableComponentProvider<MediaPlayer>.PoolableComponent => MediaPlayer;

        Type IPoolableComponentProvider<MediaPlayer>.PoolableComponentType => typeof(MediaPlayer);

        public AudioStreamComponent(PBAudioStream pbAudioStream, MediaPlayer mediaPlayer)
        {
            PBAudioStream = pbAudioStream;
            MediaPlayer = mediaPlayer;
        }

        public void Dispose()
        {
            MediaPlayer.Stop();
        }
    }
}
