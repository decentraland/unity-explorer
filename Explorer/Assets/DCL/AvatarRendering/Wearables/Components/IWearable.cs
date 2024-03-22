using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables.Helpers;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DCL.AvatarRendering.Wearables.Components
{
    public partial interface IWearable
    {
        bool IsLoading { get; set; }

        WearableType Type { get; }

        /// <summary>
        ///     Might be never resolved if Wearable is loaded from the Embedded Source
        /// </summary>
        StreamableLoadingResult<SceneAssetBundleManifest>? ManifestResult { get; set; }
        // StreamableLoadingResult<WearableAssetBase>?[] WearableAssetResults { get; }

        /// <summary>
        /// Per <see cref="BodyShape"/> [MALE, FEMALE]
        /// </summary>
        WearableAssets[] WearableAssetResults { get; }

        StreamableLoadingResult<WearableDTO> WearableDTO { get; }
        StreamableLoadingResult<Sprite>? WearableThumbnail { get; set; }

        /// <summary>
        /// DTO must be resolved only one
        /// </summary>
        void ResolveDTO(StreamableLoadingResult<WearableDTO> result);

        string GetHash();

        URN GetUrn();

        string GetName();

        string GetCategory();

        string GetDescription();

        string GetCreator();

        string GetRarity();

        bool IsUnisex();

        bool IsCompatibleWithBodyShape(string bodyShape);

        void GetHidingList(string bodyShapeType, HashSet<string> hideListResult);

        WearableDTO.WearableMetadataDto.DataDto GetData();
    }
}
