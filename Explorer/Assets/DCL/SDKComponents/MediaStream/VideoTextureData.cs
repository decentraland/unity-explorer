using DCL.SDKComponents.MediaStream;
using ECS.Unity.Textures.Components;
using System;
using UnityEngine;

namespace ECS.StreamableLoading.Textures
{
    public readonly struct VideoTextureData : IDisposable
    {
        public readonly VideoTextureConsumer Consumer;
        public readonly MediaPlayerComponent MediaPlayer;

        public VideoTextureData(VideoTextureConsumer consumer, MediaPlayerComponent mediaPlayer)
        {
            Consumer = consumer;
            MediaPlayer = mediaPlayer;
        }

        public Texture Texture => Consumer.Texture;

        public void Dispose() =>
            Consumer.Dispose();
    }
}
