using DCL.AvatarRendering.Wearables.Helpers;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;

namespace DCL.AvatarRendering.Wearables.Components
{
    public interface IWearable
    {
        string GetMainFileHash(string bodyShape);

        string GetHash();

        string GetUrn();

        string GetCategory();

        bool IsUnisex();

        bool IsCompatibleWithBodyShape(string bodyShape);

        bool IsBodyShape();

        bool IsLoading { get; set; }

        StreamableLoadingResult<SceneAssetBundleManifest>? ManifestResult { get; set; }
        StreamableLoadingResult<AssetBundleData>?[] AssetBundleData { get; set; }
        StreamableLoadingResult<WearableDTO> WearableDTO { get; set; }
    }
}
