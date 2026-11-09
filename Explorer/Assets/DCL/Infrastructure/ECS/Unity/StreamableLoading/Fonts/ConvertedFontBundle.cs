using DCL.Ipfs;

namespace ECS.StreamableLoading.Fonts
{
    public readonly struct ConvertedFontBundle
    {
        /// <summary>The content hash of the font file, not of the bundle. The bundle is requested by this hash.</summary>
        public readonly string Hash;

        /// <summary>True when the files[] of the scene manifest name a bundle for <see cref="Hash" />.</summary>
        public readonly bool Listed;

        /// <summary>The scene's asset bundle manifest, which resolves the bundle's CDN file name.</summary>
        public readonly AssetBundleManifestVersion Manifest;

        /// <summary>The id of the scene the bundle is requested for.</summary>
        public readonly string SceneId;

        public ConvertedFontBundle(string hash, bool listed, AssetBundleManifestVersion manifest, string sceneId)
        {
            Hash = hash;
            Listed = listed;
            Manifest = manifest;
            SceneId = sceneId;
        }
    }
}
