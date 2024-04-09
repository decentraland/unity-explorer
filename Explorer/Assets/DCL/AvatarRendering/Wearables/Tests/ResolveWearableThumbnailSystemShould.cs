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

    public class ResolveWearableThumbnailSystemShould : UnitySystemTestBase<ResolveAvatarAttachmentThumbnailSystem>
    {

        public void Setup()
        {
            wearableCatalog = new WearableCatalog();
            mockedDefaultAB = new StreamableLoadingResult<WearableAssetBase>(new WearableRegularAsset(null, null, null));

            IWearable mockDefaultWearable = CreateMockWearable(defaultWearableUrn, false);
            wearableCatalog.wearablesCache.Add(mockDefaultWearable.GetUrn(), mockDefaultWearable);
            realmData = new RealmData(new TestIpfsRealm());
            system = new ResolveAvatarAttachmentThumbnailSystem(world);
        }

        private StreamableLoadingResult<WearableAssetBase> mockedDefaultAB;
        private readonly string defaultWearableUrn = "urn:decentraland:off-chain:base-avatars:green_hoodie";
        private readonly string unisexTestUrn = "urn:decentraland:off-chain:base-avatars:red_hoodie_unisex";
        private WearableCatalog wearableCatalog;
        private RealmData realmData;

        private IWearable CreateMockWearable(URN urn, bool isUnisex)
        {
            IWearable wearable = Substitute.For<IWearable>();
            wearable.GetUrn().Returns(urn);
            wearable.IsUnisex().Returns(isUnisex);
            wearable.GetCategory().Returns(WearablesConstants.Categories.UPPER_BODY);
            wearable.GetThumbnail().Returns(new URLPath("bafybeie7lzqakerm4n4x7557g3va4sv7aeoniexlomdgjskuoubo6s3mku"));
            wearable.WearableDTO.Returns(new StreamableLoadingResult<WearableDTO>(new WearableDTO { id = urn }));
            return wearable;
        }


        public void ResolveWearableThumbnail()
        {
            //Arrange
            IWearable mockedWearable = CreateMockWearable(unisexTestUrn, false);
            wearableCatalog.wearablesCache.Add(mockedWearable.GetUrn(), mockedWearable);
            var urlBuilder = new URLBuilder();
            urlBuilder.AppendDomain(realmData.Ipfs.ContentBaseUrl).AppendPath(mockedWearable.GetThumbnail());

            var promise = Promise.Create(world,
                new GetTextureIntention
                {
                    CommonArguments = new CommonLoadingArguments(urlBuilder.Build()),
                }
              , PartitionComponent.TOP_PRIORITY);

            system.Update(0);

            // TODO no assertion
        }
    }
}
