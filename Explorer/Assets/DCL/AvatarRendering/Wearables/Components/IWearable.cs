using DCL.AvatarRendering.Wearables.Helpers;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Wearables.Components
{
    public interface IWearable
    {
        bool IsLoading { get; set; }

        /// <summary>
        ///     Might be never resolved if Wearable is loaded from the Embedded Source
        /// </summary>
        StreamableLoadingResult<SceneAssetBundleManifest>? ManifestResult { get; set; }
        StreamableLoadingResult<WearableAsset>?[] WearableAssets { get; }
        StreamableLoadingResult<WearableDTO> WearableDTO { get; set; }

        string GetMainFileHash(BodyShape bodyShape);

        string GetHash();

        string GetUrn();

        string GetCategory();

        bool IsUnisex();

        bool IsCompatibleWithBodyShape(string bodyShape);

        bool IsBodyShape();

        void GetHidingList(string bodyShapeType, HashSet<string> hideListResult);

        WearableDTO.WearableMetadataDto.DataDto GetData();

        bool isFacialFeature();
    }
}
