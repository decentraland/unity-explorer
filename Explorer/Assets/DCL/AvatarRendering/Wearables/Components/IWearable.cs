using DCL.AvatarRendering.Wearables.Helpers;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System;

namespace DCL.AvatarRendering.Wearables.Components
{
    public interface IWearable : IDisposable
    {
        string GetMainFileHash(BodyShape bodyShape);

        string GetHash();

        string GetUrn();

        string GetCategory();

        bool IsUnisex();

        bool IsCompatibleWithBodyShape(string bodyShape);

        bool IsBodyShape();

        bool IsLoading { get; set; }

        string[] GetHidingList();

        /// <summary>
        ///     Might be never resolved if Wearable is loaded from the Embedded Source
        /// </summary>
        StreamableLoadingResult<SceneAssetBundleManifest>? ManifestResult { get; set; }
        StreamableLoadingResult<AssetBundleData>?[] AssetBundleData { get; set; }
        StreamableLoadingResult<WearableDTO> WearableDTO { get; set; }
    }
}
