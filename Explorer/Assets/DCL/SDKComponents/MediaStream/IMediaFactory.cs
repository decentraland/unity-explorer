using Arch.Core;
using CRDT;
using ECS.StreamableLoading.Textures;
using ECS.Unity.Textures.Components;
using System.Diagnostics.CodeAnalysis;

namespace DCL.SDKComponents.MediaStream
{
    /// <summary>
    ///     This abstraction is needed solely because of Demo World
    /// </summary>
    public interface IMediaFactory
    {
        public VideoTextureData CreateVideoPlayback(string url);

        public VideoTextureConsumer CreateVideoConsumer();

        public bool TryAddConsumer(Entity consumerEntity, CRDTEntity videoPlayerCrdtEntity, [NotNullWhen(true)] out TextureData? resultData);
    }

    public class MockMediaFactory : IMediaFactory
    {
        public VideoTextureData CreateVideoPlayback(string url) =>
            new ();

        public VideoTextureConsumer CreateVideoConsumer() =>
            new ();

        public bool TryAddConsumer(Entity consumerEntity, CRDTEntity videoPlayerCrdtEntity, [NotNullWhen(true)] out TextureData? resultData)
        {
            resultData = null;
            return false;
        }

        public bool TryCreateMediaPlayer(string url, bool hasVolume, float volume, bool isSpatialAudio, float? spatialMaxDistance, out MediaPlayerComponent component)
        {
            component = default(MediaPlayerComponent);
            return false;
        }
    }
}
