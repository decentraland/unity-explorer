using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables.Helpers;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.Wearables.Components
{
    public interface IAvatarAttachment
    {
        bool IsLoading { get; set; }

        /// <summary>
        ///     Might be never resolved if Wearable is loaded from the Embedded Source
        /// </summary>
        StreamableLoadingResult<SceneAssetBundleManifest>? ManifestResult { get; set; }
        StreamableLoadingResult<WearableAsset>?[] WearableAssetResults { get; }
        StreamableLoadingResult<Sprite>? ThumbnailAssetResult { get; set; }

        string GetMainFileHash(BodyShape bodyShape);

        string GetHash();

        URN GetUrn();

        string GetName();

        string GetCategory();

        string GetDescription();

        string GetRarity();

        URLPath GetThumbnail();

        bool IsUnisex();

        bool IsCompatibleWithBodyShape(string bodyShape);

        bool IsBodyShape();

        void GetHidingList(string bodyShapeType, HashSet<string> hideListResult);

        bool IsFacialFeature();
    }
}
