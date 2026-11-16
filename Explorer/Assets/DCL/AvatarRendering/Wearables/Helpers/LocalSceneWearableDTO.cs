namespace DCL.AvatarRendering.Wearables.Helpers
{
    public sealed class LocalSceneWearableDTO : WearableDTO
    {
        /// <summary>A bundle that fails to load, or loads without a GameObject, is re-requested as the raw GLTF.</summary>
        public bool RawGltfFallback { get; }

        public LocalSceneWearableDTO(string? contentDownloadUrl, bool rawGltfFallback)
        {
            ContentDownloadUrl = contentDownloadUrl;
            RawGltfFallback = rawGltfFallback;
        }
    }
}
