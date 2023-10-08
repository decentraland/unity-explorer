using DCL.AvatarRendering.Wearables.Helpers;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System;
using System.Linq;

//Removed all references to EmoteData in WearableItem
namespace DCL.AvatarRendering.Wearables.Components
{
    [Serializable]
    public class Wearable : IWearable
    {
        public StreamableLoadingResult<SceneAssetBundleManifest>? ManifestResult { get; set; }
        public StreamableLoadingResult<AssetBundleData>?[] AssetBundleData { get; set; }
        public StreamableLoadingResult<WearableDTO> WearableDTO { get; set; }
        public bool IsLoading { get; set; }

        public Wearable()
        {
            IsLoading = true;

            AssetBundleData = new StreamableLoadingResult<AssetBundleData>?[BodyShape.COUNT];

            for (var i = 0; i < AssetBundleData.Length; i++)
                AssetBundleData[i] = null;
        }

        public string GetMainFileHash(BodyShape bodyShape)
        {
            var mainFileKey = "";
            var hashToReturn = "";

            // The length of arrays is small, so O(N) complexity is fine
            // Avoid iterator allocations with "for" loop
            for (var i = 0; i < WearableDTO.Asset.metadata.data.representations.Length; i++)
            {
                WearableDTO.WearableMetadataDto.Representation representation = WearableDTO.Asset.metadata.data.representations[i];

                if (representation.bodyShapes.Contains(bodyShape))
                {
                    mainFileKey = representation.mainFile;
                    break;
                }
            }

            for (var i = 0; i < WearableDTO.Asset.content.Length; i++)
            {
                WearableDTO.WearableContentDto wearableContentDto = WearableDTO.Asset.content[i];

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

        public bool IsUnisex() =>
            WearableDTO.Asset.metadata.data.representations.Length > 1;

        public string[] GetHidingList()
        {
            if (WearableDTO.Asset.metadata.data.hides == null)
                return Array.Empty<string>();

            return WearableDTO.Asset.metadata.data.hides;
        }

        public bool IsCompatibleWithBodyShape(string bodyShape)
        {
            foreach (WearableDTO.WearableMetadataDto.Representation dataRepresentation in WearableDTO.Asset.metadata.data.representations)
            {
                if (dataRepresentation.bodyShapes.Contains(bodyShape))
                    return true;
            }

            return false;
        }

        public bool IsBodyShape() =>
            GetCategory().Equals(WearablesConstants.Categories.BODY_SHAPE);

        //TODO: Implement Dispose method
        public void Dispose() { }
    }
}
