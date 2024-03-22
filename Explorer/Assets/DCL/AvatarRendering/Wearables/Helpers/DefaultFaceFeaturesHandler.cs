using System.Collections.Generic;
using DCL.WebRequests;
using UnityEngine;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    public class DefaultFaceFeaturesHandler : IDefaultFaceFeaturesHandler
    {
        private readonly IWearableCatalog wearableCatalog;
        private readonly Dictionary<string, Texture>[] defaultFacialFeaturesDictionary = new Dictionary<string, Texture>[2];

        private readonly Texture[] defaultEyes;
        private readonly Texture[] defaultMouths;
        private readonly Texture[] defaultEyebrows;

        private readonly bool isInitialized = false;

        public DefaultFaceFeaturesHandler(IWearableCatalog wearableCatalog)
        {
            this.wearableCatalog = wearableCatalog;
            defaultFacialFeaturesDictionary = new Dictionary<string, Texture>[2];
            defaultEyes = new Texture[2];
            defaultMouths = new Texture[2];
            defaultEyebrows = new Texture[2];
        }

        public Dictionary<string, Texture> GetDefaultFacialFeaturesDictionary(BodyShape bodyShape)
        {
            if (!isInitialized)
                Initialize();

            ResetDictionary(bodyShape);

            return defaultFacialFeaturesDictionary[bodyShape];
        }

        private void ResetDictionary(BodyShape bodyShape)
        {
            defaultFacialFeaturesDictionary[bodyShape][WearablesConstants.Categories.EYES] = defaultEyes[bodyShape];
            defaultFacialFeaturesDictionary[bodyShape][WearablesConstants.Categories.MOUTH] = defaultMouths[bodyShape];
            defaultFacialFeaturesDictionary[bodyShape][WearablesConstants.Categories.EYEBROWS] = defaultEyebrows[bodyShape];
        }

        private void Initialize()
        {
            foreach (var bodyShape in BodyShape.VALUES)
            {
                defaultEyes[bodyShape] = wearableCatalog.GetDefaultWearable(bodyShape, WearablesConstants.Categories.EYES, out bool hasEmptyDefaultWearableAB_1).WearableAssetResults[bodyShape].Value.Asset.GetMainAsset<Texture>();
                defaultMouths[bodyShape] = wearableCatalog.GetDefaultWearable(bodyShape, WearablesConstants.Categories.MOUTH, out bool hasEmptyDefaultWearableAB_2).WearableAssetResults[bodyShape].Value.Asset.GetMainAsset<Texture>();
                defaultEyebrows[bodyShape] = wearableCatalog.GetDefaultWearable(bodyShape, WearablesConstants.Categories.EYEBROWS, out bool hasEmptyDefaultWearableAB_3).WearableAssetResults[bodyShape].Value.Asset.GetMainAsset<Texture>();

                var facialFeatureTexture = new Dictionary<string, Texture>();
                facialFeatureTexture.Add(WearablesConstants.Categories.EYES, defaultEyes[bodyShape]);
                facialFeatureTexture.Add(WearablesConstants.Categories.MOUTH, defaultMouths[bodyShape]);
                facialFeatureTexture.Add(WearablesConstants.Categories.EYEBROWS, defaultEyebrows[bodyShape]);
                defaultFacialFeaturesDictionary[bodyShape] = facialFeatureTexture;
            }
        }
    }


}