using Arch.Core;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.DTO;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.AvatarRendering.Wearables.Systems;
using DCL.Multiplayer.Connections.DecentralandUrls;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using Runtime.Wearables;
using System;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Wearables.Tests
{
    public class ResolveWearablePromisesSystemShould : UnitySystemTestBase<ResolveWearablePromisesSystem>
    {
        private const string WEARABLE_URN = "urn:decentraland:off-chain:base-avatars:red_hoodie";

        [SetUp]
        public void SetUp()
        {
            var wearableStorage = new WearableStorage();
            wearableStorage.Set(new URN(WEARABLE_URN), new Wearable(new StreamableLoadingResult<WearableDTO>(CreateDTO())));

            system = new ResolveWearablePromisesSystem(world, wearableStorage, Substitute.For<IDecentralandUrlsSource>(), URLSubdirectory.EMPTY);
        }

        [Test]
        public void ResolveADtoOnlyRequestWithoutWaitingForAssets()
        {
            // Arrange
            Entity entity = CreateIntention(dtoOnly: true);

            // Act
            system.Update(0);

            // Assert
            Assert.IsTrue(world.Has<StreamableLoadingResult<WearablesResolution>>(entity));
            StreamableLoadingResult<WearablesResolution> result = world.Get<StreamableLoadingResult<WearablesResolution>>(entity);
            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(1, result.Asset.Wearables.Count);
        }

        [Test]
        public void WaitForTheAssetsOfARegularRequest()
        {
            // Arrange
            Entity entity = CreateIntention(dtoOnly: false);

            // Act
            system.Update(0);

            // Assert
            Assert.IsFalse(world.Has<StreamableLoadingResult<WearablesResolution>>(entity));
        }

        private Entity CreateIntention(bool dtoOnly) =>
            world.Create(new GetWearablesByPointersIntention(new List<URN> { new (WEARABLE_URN) }, BodyShape.MALE, Array.Empty<string>(), dtoOnly: dtoOnly),
                (IPartitionComponent)PartitionComponent.TOP_PRIORITY);

        private static WearableDTO CreateDTO() =>
            new ()
            {
                id = WEARABLE_URN,
                metadata = new WearableDTO.WearableMetadataDto
                {
                    id = WEARABLE_URN,
                    data = new WearableDTO.WearableMetadataDto.DataDto
                    {
                        category = WearableCategories.Categories.UPPER_BODY,
                        hides = Array.Empty<string>(),
                        replaces = Array.Empty<string>(),
                        representations = new[]
                        {
                            new AvatarAttachmentDTO.Representation
                            {
                                bodyShapes = new string[] { BodyShape.MALE },
                                overrideHides = Array.Empty<string>(),
                                overrideReplaces = Array.Empty<string>(),
                            },
                        },
                    },
                },
            };
    }
}
