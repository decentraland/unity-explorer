using CommunicationData.URLHelpers;
using DCL.AvatarRendering.AvatarShape.Tests.EditMode;
using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.DTO;
using DCL.AvatarRendering.Thumbnails.Systems;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using ECS;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Tests;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using ECS.TestSuite;
using NUnit.Framework;
using System.Collections.Generic;
using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.Texture2D, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace DCL.AvatarRendering.Wearables.Tests
{
    [TestFixture]
    public class ResolveWearableThumbnailSystemShould : UnitySystemTestBase<ResolveAvatarAttachmentThumbnailSystem>
    {
        [SetUp]
        public void Setup()
        {
            wearableStorage = new WearableStorage();
            mockedDefaultAB = new StreamableLoadingResult<AttachmentAssetBase>(new AttachmentRegularAsset(null, null, null));

            IWearable mockDefaultWearable = CreateMockWearable(defaultWearableUrn, false);
            wearableStorage.wearablesCache.Add(mockDefaultWearable.GetUrn(), mockDefaultWearable);
            realmData = new RealmData(new TestIpfsRealm());
            system = new ResolveAvatarAttachmentThumbnailSystem(world);
        }

        private StreamableLoadingResult<AttachmentAssetBase> mockedDefaultAB;
        private readonly string defaultWearableUrn = "urn:decentraland:off-chain:base-avatars:green_hoodie";
        private readonly string unisexTestUrn = "urn:decentraland:off-chain:base-avatars:red_hoodie_unisex";
        private WearableStorage wearableStorage;
        private RealmData realmData;

        private static IWearable CreateMockWearable(URN urn, bool isUnisex) =>
            new FakeWearable(
                new WearableDTO
                {
                    metadata = new WearableDTO.WearableMetadataDto
                    {
                        id = urn,
                        thumbnail = "bafybeie7lzqakerm4n4x7557g3va4sv7aeoniexlomdgjskuoubo6s3mku",
                        data =
                        {
                            representations = isUnisex
                                ? new AvatarAttachmentDTO.Representation[] { new (), new () }
                                : new AvatarAttachmentDTO.Representation[] { new () },
                            category = WearablesConstants.Categories.UPPER_BODY,
                        },
                    },
                },
                model: new StreamableLoadingResult<WearableDTO>(new WearableDTO { id = urn })
            );

        [Test]
        public void ResolveWearableThumbnail()
        {
            //Arrange
            IWearable mockedWearable = CreateMockWearable(unisexTestUrn, false);
            wearableStorage.wearablesCache.Add(mockedWearable.GetUrn(), mockedWearable);
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
