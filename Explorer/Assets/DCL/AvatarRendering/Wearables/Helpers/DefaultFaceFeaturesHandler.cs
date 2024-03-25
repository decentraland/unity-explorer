using System.Collections.Generic;
using DCL.WebRequests;
using UnityEngine;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    public class DefaultFaceFeaturesHandler : IDefaultFaceFeaturesHandler
    {
        private readonly IWearableCatalog wearableCatalog;

        private readonly FacialFeaturesTextures[] sharedFacialFeatures = new FacialFeaturesTextures[BodyShape.COUNT];

        private readonly Dictionary<string, Texture>[] defaultMainTextures = new Dictionary<string, Texture>[BodyShape.COUNT];

        private readonly bool isInitialized = false;

        public DefaultFaceFeaturesHandler(IWearableCatalog wearableCatalog)
        {
            this.wearableCatalog = wearableCatalog;
        }

        public FacialFeaturesTextures GetDefaultFacialFeaturesDictionary(BodyShape bodyShape)
        {
            if (!isInitialized)
                Initialize();

            ResetDictionary(bodyShape);

            return sharedFacialFeatures[bodyShape];
        }

        private void ResetDictionary(BodyShape bodyShape)
        {
            var dict = sharedFacialFeatures[bodyShape].Value;

            foreach (Dictionary<int,Texture> innerDict in dict.Values)
                innerDict.Clear();

            var defTextures = defaultMainTextures[bodyShape];

            foreach (string facialFeature in WearablesConstants.FACIAL_FEATURES)
                dict[facialFeature][WearableTextureConstants.MAINTEX_ORIGINAL_TEXTURE] = defTextures[facialFeature];
        }

        private void Initialize()
        {
            foreach (var bodyShape in BodyShape.VALUES)
            {
                var mainTexForThisBodyShape = new Dictionary<string, Texture>();
                this.defaultMainTextures[bodyShape] = mainTexForThisBodyShape;
                var facialFeatureDict = new Dictionary<string, Dictionary<int, Texture>>();
                sharedFacialFeatures[bodyShape] = new FacialFeaturesTextures(facialFeatureDict);

                foreach (string facialFeature in WearablesConstants.FACIAL_FEATURES)
                {
                    // TODO it's quite dangerous to call it like this without any checks
                    var result = (WearableTextureAsset) wearableCatalog.GetDefaultWearable(bodyShape, facialFeature)
                                                .WearableAssetResults[bodyShape]
                                                .Results[WearablePolymorphicBehaviour.MAIN_ASSET_INDEX]!
                                                .Value.Asset;

                    mainTexForThisBodyShape[facialFeature] = result.Texture;

                    var innerDict = new Dictionary<int, Texture>();
                    innerDict[WearableTextureConstants.MAINTEX_ORIGINAL_TEXTURE] = result.Texture;
                    facialFeatureDict.Add(facialFeature, innerDict);
                }
            }
        }
    }


}
