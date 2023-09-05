using ECS.StreamableLoading.AssetBundles;
using SceneRunner.Scene;
using System;
using UnityEngine;

//Removed all references to EmoteData in WearableItem
namespace DCL.AvatarRendering.Wearables.Components
{
    [Serializable]
    public struct WearableComponent
    {

        /// <summary>
        ///     The current status of the Wearable loading
        /// </summary>
        public AssetBundleData AssetBundleData;

        public SceneAssetBundleManifest AssetBundleManifest;

        //CONTENT
        public WearableContent wearableContent;

        public string urn; // urn
        public string baseUrl;

        public string hash; //hash
        public string rarity;
        public string description;
        public i18n[] i18n;
        public string thumbnailHash;
        public Sprite thumbnailSprite;

        public string GetMainFileHash()
        {
            //TODO: Get main file hash depending on the representation
            //int representationIndex = bodyShape.Equals(WearablesLiterals.BodyShapes.MALE) ? 0 : 1;
            string mainFileKey = wearableContent.representations[0].mainFile;
            foreach (WearableMappingPair wearableMappingPair in wearableContent.representations[0].contents)
            {
                if (wearableMappingPair.key.Equals(mainFileKey))
                    return wearableMappingPair.hash;
            }
            return "";
        }
    }

    [Serializable]
    public class i18n
    {
        public string code;
        public string text;
    }

    [Serializable]
    public class WearableMappingPair
    {
        public string key;
        public string hash;
        public string url;
    }

    [Serializable]
    public class WearableRepresentation
    {
        public string[] bodyShapes;
        public string mainFile;
        public WearableMappingPair[] contents;
        public string[] overrideHides;
        public string[] overrideReplaces;
    }

    [Serializable]
    public class WearableContent
    {
        public WearableRepresentation[] representations;
        public string category;
        public string[] tags;
        public string[] replaces;
        public string[] hides;
        public string[] removesDefaultHiding;
        public bool loop;
    }
}
