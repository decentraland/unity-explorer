using System.Collections.Generic;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using ECS.StreamableLoading.Common.Components;
using Google.Protobuf.WellKnownTypes;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace DCL.AvatarRendering.Wearables.Tests
{
    public class DefaultFaceFeatureHandlerShould
    {
        private IDefaultFaceFeaturesHandler defaultFaceFeaturesHandler;

        private IWearableCatalog wearableCatalog;

        private Texture eyesTexture;
        private Texture mouthTexture;
        private Texture eyebrowsTexture;

        [SetUp]
        public void SetUp()
        {
            wearableCatalog = Substitute.For<IWearableCatalog>();

            eyesTexture = CreateFacialFeatureWearable(1, WearablesConstants.Categories.EYES);
            mouthTexture = CreateFacialFeatureWearable(2, WearablesConstants.Categories.MOUTH);
            eyebrowsTexture = CreateFacialFeatureWearable(3, WearablesConstants.Categories.EYEBROWS);

            defaultFaceFeaturesHandler = new DefaultFaceFeaturesHandler(wearableCatalog);
        }

        private Texture CreateFacialFeatureWearable(int resolution, string category)
        {
            var mock = Substitute.For<IWearable>();
            var tex = new Texture2D(resolution, resolution);

            var main = new StreamableLoadingResult<WearableAssetBase>(new WearableTextureAsset(tex, null));
            var mask = new StreamableLoadingResult<WearableAssetBase>((WearableTextureAsset)null); // no mask

            var array = new WearableAssets[BodyShape.COUNT];

            for (var i = 0; i < array.Length; i++)
            {
                var innerArray = new StreamableLoadingResult<WearableAssetBase>?[2];
                innerArray[WearablePolymorphicBehaviour.MAIN_ASSET_INDEX] = main;
                innerArray[WearablePolymorphicBehaviour.MASK_ASSET_INDEX] = mask;

                array[i] = new WearableAssets { Results = innerArray };
            }

            mock.WearableAssetResults.Returns(array);

            wearableCatalog.GetDefaultWearable(Arg.Any<BodyShape>(), category).Returns(mock);

            return tex;
        }

        [Test]
        public void GetDefaultTextures()
        {
            var defaultFacialFeaturesDictionary = defaultFaceFeaturesHandler.GetDefaultFacialFeaturesDictionary(BodyShape.MALE);

            Assert.AreEqual(defaultFacialFeaturesDictionary[WearablesConstants.Categories.EYES, WearableTextureConstants.MAINTEX_ORIGINAL_TEXTURE], eyesTexture);
            Assert.AreEqual(defaultFacialFeaturesDictionary[WearablesConstants.Categories.MOUTH, WearableTextureConstants.MAINTEX_ORIGINAL_TEXTURE], mouthTexture);
            Assert.AreEqual(defaultFacialFeaturesDictionary[WearablesConstants.Categories.EYEBROWS, WearableTextureConstants.MAINTEX_ORIGINAL_TEXTURE], eyebrowsTexture);
        }

        [Test]
        public void GetDefaultTexturesAfterReplaced()
        {
            var defaultFacialFeaturesDictionary = defaultFaceFeaturesHandler.GetDefaultFacialFeaturesDictionary(BodyShape.MALE);

            defaultFacialFeaturesDictionary.Value[WearablesConstants.Categories.EYES][WearableTextureConstants.MAINTEX_ORIGINAL_TEXTURE] = new Texture2D(4, 4);

            defaultFacialFeaturesDictionary = defaultFaceFeaturesHandler.GetDefaultFacialFeaturesDictionary(BodyShape.MALE);

            Assert.AreEqual(defaultFacialFeaturesDictionary[WearablesConstants.Categories.EYES, WearableTextureConstants.MAINTEX_ORIGINAL_TEXTURE], eyesTexture);
            Assert.AreEqual(defaultFacialFeaturesDictionary[WearablesConstants.Categories.MOUTH, WearableTextureConstants.MAINTEX_ORIGINAL_TEXTURE], mouthTexture);
            Assert.AreEqual(defaultFacialFeaturesDictionary[WearablesConstants.Categories.EYEBROWS, WearableTextureConstants.MAINTEX_ORIGINAL_TEXTURE], eyebrowsTexture);
        }
    }
}
