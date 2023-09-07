using DCL.AvatarRendering.Wearables.Helpers;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Linq;

//Removed all references to EmoteData in WearableItem
namespace DCL.AvatarRendering.Wearables.Components
{
    [Serializable]
    public class Wearable
    {

        public string urn; // urn

        public StreamableLoadingResult<SceneAssetBundleManifest>? ManifestResult;
        public Dictionary<string, StreamableLoadingResult<AssetBundleData>?> AssetBundleData;
        public StreamableLoadingResult<WearableDTO> WearableDTO;

        public bool IsLoading;

        public Wearable(string urn)
        {
            this.urn = urn;
            AssetBundleData = new Dictionary<string, StreamableLoadingResult<AssetBundleData>?>();

            foreach (string bodyShape in WearablesLiterals.BodyShapes.GetBodyShapes()) { AssetBundleData.Add(bodyShape, null); }
        }

        //TODO: Make this method better
        public string GetMainFileHash(string bodyShape)
        {
            var mainFileKey = "";
            var hashToReturn = "";

            foreach (WearableDTO.WearableMetadataDto.Representation representation in WearableDTO.Asset.metadata.data.representations)
            {
                if (representation.bodyShapes.Contains(bodyShape))
                {
                    mainFileKey = representation.mainFile;
                    break;
                }
            }

            foreach (WearableDTO.WearableContentDto wearableContentDto in WearableDTO.Asset.content)
            {
                if (wearableContentDto.file.Equals(mainFileKey))
                {
                    hashToReturn = wearableContentDto.hash;
                    break;
                }
            }

            return hashToReturn;
        }

        public string GetHash() =>
            WearableDTO.Asset.id;

        public string GetUrn() =>
            WearableDTO.Asset.metadata.id;

        public string GetCategory() =>
            WearableDTO.Asset.metadata.data.category;
    }

}
