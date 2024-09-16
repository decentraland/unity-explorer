using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using ECS.StreamableLoading.Common.Components;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace DCL.AvatarRendering.Wearables.Tests
{
    public class DefaultFaceFeatureHandlerShould
    {
        private IDefaultFaceFeaturesHandler defaultFaceFeaturesHandler;

        private IWearableStorage wearableStorage;

        private Texture eyesTexture;
        private Texture mouthTexture;
        private Texture eyebrowsTexture;

        [SetUp]
        public void SetUp()
        {
            wearableStorage = Substitute.For<IWearableStorage>();

            eyesTexture = CreateFacialFeatureWearable(1, WearablesConstants.Categories.EYES);
            mouthTexture = CreateFacialFeatureWearable(2, WearablesConstants.Categories.MOUTH);
            eyebrowsTexture = CreateFacialFeatureWearable(3, WearablesConstants.Categories.EYEBROWS);

            defaultFaceFeaturesHandler = new DefaultFaceFeaturesHandler(wearableStorage);
        }

        private Texture CreateFacialFeatureWearable(int resolution, string category)
        {
            var mock = Substitute.For<IWearable>();
            var tex = new Texture2D(resolution, resolution);

            var main = new StreamableLoadingResult<AttachmentAssetBase>(new AttachmentTextureAsset(tex, null));
            var mask = new StreamableLoadingResult<AttachmentAssetBase>((AttachmentTextureAsset)null); // no mask

            var array = new WearableAssets[BodyShape.COUNT];

            for (var i = 0; i < array.Length; i++)
            {
                var innerArray = new StreamableLoadingResult<AttachmentAssetBase>?[2];
                innerArray[WearablePolymorphicBehaviour.MAIN_ASSET_INDEX] = main;
                innerArray[WearablePolymorphicBehaviour.MASK_ASSET_INDEX] = mask;

                array[i] = new WearableAssets { Results = innerArray };
            }

            mock.WearableAssetResults.Returns(array);

            wearableStorage.GetDefaultWearable(Arg.Any<BodyShape>(), category).Returns(mock);

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
