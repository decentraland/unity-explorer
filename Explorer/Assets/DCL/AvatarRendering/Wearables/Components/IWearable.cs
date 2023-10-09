using DCL.AvatarRendering.Wearables.Helpers;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System;

namespace DCL.AvatarRendering.Wearables.Components
{
    public interface IWearable : IDisposable
    {
        bool IsLoading { get; set; }

        /// <summary>
        ///     Might be never resolved if Wearable is loaded from the Embedded Source
        /// </summary>
        StreamableLoadingResult<SceneAssetBundleManifest>? ManifestResult { get; set; }
        StreamableLoadingResult<WearableAsset>?[] WearableAssets { get; set; }
        StreamableLoadingResult<WearableDTO> WearableDTO { get; set; }

        string GetMainFileHash(BodyShape bodyShape);

        string GetHash();

        string GetUrn();

        string GetCategory();

        bool IsUnisex();

        bool IsCompatibleWithBodyShape(string bodyShape);

        bool IsBodyShape();

        string[] GetHidingList();
    }
}
