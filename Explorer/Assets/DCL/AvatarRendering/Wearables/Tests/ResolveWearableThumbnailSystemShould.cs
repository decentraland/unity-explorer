using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.AvatarRendering.Wearables.Systems;
using ECS;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Tests;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.Texture2D, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace DCL.AvatarRendering.Wearables.Tests
{
    [TestFixture]
    public class ResolveWearableThumbnailSystemShould : UnitySystemTestBase<ResolveWearableThumbnailSystem>
    {
        private StreamableLoadingResult<WearableAssetBase> mockedDefaultAB;
        private readonly string defaultWearableUrn = "urn:decentraland:off-chain:base-avatars:green_hoodie";
        private readonly string unisexTestUrn = "urn:decentraland:off-chain:base-avatars:red_hoodie_unisex";
        private WearableCatalog wearableCatalog;
        private RealmData realmData;

        [SetUp]
        public void Setup()
        {
            wearableCatalog = new WearableCatalog();
            IWearable mockDefaultWearable = CreateMockWearable(defaultWearableUrn, false, true);
            wearableCatalog.wearablesCache.Add(mockDefaultWearable.GetUrn(), mockDefaultWearable);
            mockedDefaultAB = new StreamableLoadingResult<WearableAssetBase>(new WearableAssetBase(null, null, null));
            realmData = new RealmData(new TestIpfsRealm());
            system = new ResolveWearableThumbnailSystem(world);
        }

        private IWearable CreateMockWearable(URN urn, bool isUnisex, bool isDefaultWearable)
        {
            IWearable wearable = Substitute.For<IWearable>();
            wearable.GetUrn().Returns(urn);
            wearable.IsUnisex().Returns(isUnisex);
            wearable.GetCategory().Returns(WearablesConstants.Categories.UPPER_BODY);
            wearable.GetThumbnail().Returns(new URLPath("bafybeie7lzqakerm4n4x7557g3va4sv7aeoniexlomdgjskuoubo6s3mku"));

            var assetBundleData
                = new StreamableLoadingResult<WearableAssetBase>?[BodyShape.COUNT];

            if (isDefaultWearable)
                assetBundleData[BodyShape.MALE] = mockedDefaultAB;

            wearable.WearableAssetResults.Returns(assetBundleData);
            return wearable;
        }


        [Test]
        public void ResolveWearableThumbnail()
        {
            //Arrange
            IWearable mockedWearable = CreateMockWearable(unisexTestUrn, false, true);
            wearableCatalog.wearablesCache.Add(mockedWearable.GetUrn(), mockedWearable);
            URLBuilder urlBuilder = new URLBuilder();
            urlBuilder.AppendDomain(realmData.Ipfs.ContentBaseUrl).AppendPath(mockedWearable.GetThumbnail());

            Promise promise = Promise.Create(world,
                new GetTextureIntention
                {
                    CommonArguments = new CommonLoadingArguments(urlBuilder.Build())
                }
              , PartitionComponent.TOP_PRIORITY);
            system.Update(0);
        }

    }
}
