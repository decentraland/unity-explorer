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

        /// <summary>
        ///     Registers a consumer that draws the video texture over the whole screen without a renderer (the scene skybox)
        ///     and takes a reference on the returned <see cref="TextureData" />.
        /// </summary>
        /// <returns>False while the video player entity is not ready yet.</returns>
        public bool TryAddScreenSpaceConsumer(CRDTEntity videoPlayerCrdtEntity, [NotNullWhen(true)] out TextureData? resultData);

        /// <summary>
        ///     Unregisters a consumer added with <see cref="TryAddScreenSpaceConsumer" />. No-op when the video player entity is gone.
        ///     The <see cref="TextureData" /> reference taken on registration is not released here.
        /// </summary>
        public void RemoveScreenSpaceConsumer(CRDTEntity videoPlayerCrdtEntity);

        /// <summary>
        ///     Whether the video player entity still exists and owns its video texture. False once the entity is deleted:
        ///     its render texture goes back to the shared pool, so a texture obtained from it must not be used any more.
        /// </summary>
        public bool HasVideoTexture(CRDTEntity videoPlayerCrdtEntity);
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

        public bool TryAddScreenSpaceConsumer(CRDTEntity videoPlayerCrdtEntity, [NotNullWhen(true)] out TextureData? resultData)
        {
            resultData = null;
            return false;
        }

        public void RemoveScreenSpaceConsumer(CRDTEntity videoPlayerCrdtEntity) { }

        public bool HasVideoTexture(CRDTEntity videoPlayerCrdtEntity) =>
            false;

        public bool TryCreateMediaPlayer(string url, bool hasVolume, float volume, bool isSpatialAudio, float? spatialMaxDistance, out MediaPlayerComponent component)
        {
            component = default(MediaPlayerComponent);
            return false;
        }
    }
}
