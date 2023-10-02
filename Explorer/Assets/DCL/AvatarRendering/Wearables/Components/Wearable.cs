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

            AssetBundleData = new StreamableLoadingResult<AssetBundleData>?[WearablesLiterals.BodyShape.COUNT];

            for (var i = 0; i < AssetBundleData.Length; i++)
                AssetBundleData[i] = null;
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
            GetCategory().Equals(WearablesLiterals.Categories.BODY_SHAPE);

        //TODO: Implement Dispose method
        public void Dispose() { }

    }
}
