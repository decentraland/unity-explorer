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

        private Texture eyesTexture;
        private Texture mouthTexture;
        private Texture eyebrowsTexture;


        [SetUp]
        public void SetUp()
        {
            var wearableCatalog = Substitute.For<IWearableCatalog>();

            var mockEyes = Substitute.For<IWearable>();
            eyesTexture = new Texture2D(1, 1);
            var eyes = new StreamableLoadingResult<WearableAssetBase>(new WearableAssetBase(eyesTexture, null, null));
            mockEyes.WearableAssetResults.Returns(new StreamableLoadingResult<WearableAssetBase>?[2]
            {
                eyes, eyes
            });

            var mockMouth = Substitute.For<IWearable>();
            mouthTexture = new Texture2D(2, 2);
            var mouth = new StreamableLoadingResult<WearableAssetBase>(new WearableAssetBase(mouthTexture, null, null));
            mockMouth.WearableAssetResults.Returns(new StreamableLoadingResult<WearableAssetBase>?[2]
            {
                mouth, mouth
            });

            var mockEyebros = Substitute.For<IWearable>();
            eyebrowsTexture = new Texture2D(3, 3);
            var eyebros = new StreamableLoadingResult<WearableAssetBase>(new WearableAssetBase(eyebrowsTexture, null, null));
            mockEyebros.WearableAssetResults.Returns(new StreamableLoadingResult<WearableAssetBase>?[2]
            {
                eyebros, eyebros
            });


            wearableCatalog.GetDefaultWearable(Arg.Any<BodyShape>(), WearablesConstants.Categories.EYES, out Arg.Any<bool>()).Returns(mockEyes);
            wearableCatalog.GetDefaultWearable(Arg.Any<BodyShape>(), WearablesConstants.Categories.EYEBROWS, out Arg.Any<bool>()).Returns(mockEyebros);
            wearableCatalog.GetDefaultWearable(Arg.Any<BodyShape>(), WearablesConstants.Categories.MOUTH, out Arg.Any<bool>()).Returns(mockMouth);


            defaultFaceFeaturesHandler = new DefaultFaceFeaturesHandler(wearableCatalog);
        }


        [Test]
        public void GetDefaultTextures()
        {
            var defaultFacialFeaturesDictionary = defaultFaceFeaturesHandler.GetDefaultFacialFeaturesDictionary(BodyShape.MALE);

            Assert.AreEqual(defaultFacialFeaturesDictionary[WearablesConstants.Categories.EYES], eyesTexture);
            Assert.AreEqual(defaultFacialFeaturesDictionary[WearablesConstants.Categories.MOUTH], mouthTexture);
            Assert.AreEqual(defaultFacialFeaturesDictionary[WearablesConstants.Categories.EYEBROWS], eyebrowsTexture);
        }

        [Test]
        public void GetDefaultTexturesAfterReplaced()
        {
            var defaultFacialFeaturesDictionary = defaultFaceFeaturesHandler.GetDefaultFacialFeaturesDictionary(BodyShape.MALE);

            defaultFacialFeaturesDictionary[WearablesConstants.Categories.EYES] = new Texture2D(4, 4);

            defaultFacialFeaturesDictionary = defaultFaceFeaturesHandler.GetDefaultFacialFeaturesDictionary(BodyShape.MALE);

            Assert.AreEqual(defaultFacialFeaturesDictionary[WearablesConstants.Categories.EYES], eyesTexture);
            Assert.AreEqual(defaultFacialFeaturesDictionary[WearablesConstants.Categories.MOUTH], mouthTexture);
            Assert.AreEqual(defaultFacialFeaturesDictionary[WearablesConstants.Categories.EYEBROWS], eyebrowsTexture);
        }
    }
}