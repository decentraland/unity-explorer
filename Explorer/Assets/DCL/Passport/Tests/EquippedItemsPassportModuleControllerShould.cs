using Arch.Core;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.DTO;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.AvatarRendering.Wearables.Systems;
using DCL.Ipfs;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Passport.Modules;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using Runtime.Wearables;
using System;
using System.Collections.Generic;
using UnityEngine;
using Avatar = DCL.Profiles.Avatar;

namespace DCL.Passport.Tests
{
    public class EquippedItemsPassportModuleControllerShould : UnitySystemTestBase<ResolveWearablePromisesSystem>
    {
        private const string SHIRT_URN = "urn:decentraland:off-chain:base-avatars:red_hoodie";

        private WearableStorage wearableStorage = null!;

        [SetUp]
        public void SetUp()
        {
            wearableStorage = new WearableStorage();

            var urls = Substitute.For<IDecentralandUrlsSource>();
            urls.Url(DecentralandUrl.EntitiesActiveElements).Returns("http://localhost/entities/active");
            system = new ResolveWearablePromisesSystem(world, wearableStorage, urls, URLSubdirectory.EMPTY);
        }

        [Test]
        public void ListEquippedWearablesWhoseAssetsCannotBeDownloaded()
        {
            // Arrange: the definitions are known, but the bundle CDN is unreachable, so no 3D asset can load
            IWearable body = AddDefinitionWithUnreachableAssets(BodyShape.MALE, WearableCategories.Categories.BODY_SHAPE);
            IWearable shirt = AddDefinitionWithUnreachableAssets(SHIRT_URN, WearableCategories.Categories.UPPER_BODY);
            var avatar = new Avatar(BodyShape.MALE, new HashSet<URN> { new (SHIRT_URN) }, Color.white, Color.white, Color.white);

            Entity request = world.Create(EquippedItemsPassportModuleController.CreateEquippedWearablesIntention(avatar),
                (IPartitionComponent)PartitionComponent.TOP_PRIORITY);

            // Act
            system.Update(0);

            // Assert
            Assert.That(world.TryGet(request, out StreamableLoadingResult<WearablesResolution> result), Is.True);
            Assert.That(result.Succeeded, Is.True);
            CollectionAssert.AreEquivalent(new[] { body, shirt }, result.Asset.Wearables);
            result.Asset.Dispose();
        }

        private IWearable AddDefinitionWithUnreachableAssets(string urn, string category) =>
            wearableStorage.GetOrAddByDTO(new WearableDTO
            {
                id = urn,
                assetBundleManifestVersion = AssetBundleManifestVersion.FAILED,
                content = new[] { new ContentDefinition { file = "model.glb", hash = "model-hash" } },
                metadata = new WearableDTO.WearableMetadataDto
                {
                    id = urn,
                    data = new WearableDTO.WearableMetadataDto.DataDto
                    {
                        category = category,
                        hides = Array.Empty<string>(),
                        replaces = Array.Empty<string>(),
                        representations = new[]
                        {
                            new AvatarAttachmentDTO.Representation
                            {
                                mainFile = "model.glb",
                                contents = new[] { "model.glb" },
                                bodyShapes = new[] { BodyShape.MALE.Value },
                                overrideHides = Array.Empty<string>(),
                                overrideReplaces = Array.Empty<string>(),
                            },
                        },
                    },
                },
            });
    }
}
