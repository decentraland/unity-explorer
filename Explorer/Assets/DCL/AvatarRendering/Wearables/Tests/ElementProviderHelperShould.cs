using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using ECS.StreamableLoading.Common.Components;
using NUnit.Framework;
using System;

namespace DCL.AvatarRendering.Wearables.Tests
{
    public class ElementProviderHelperShould
    {
        [Test]
        public void TreatStoredWearableWithLoadedDTOAsResolved()
        {
            var model = new WearableDTO
            {
                metadata = new WearableDTO.WearableMetadataDto
                {
                    id = "urn:decentraland:off-chain:base-avatars:aviatorstyle",
                    data = new WearableDTO.WearableMetadataDto.DataDto { category = "hat" },
                },
            };

            IWearable wearable = new Wearable(new StreamableLoadingResult<WearableDTO>(model));

            Assert.IsTrue(ElementProviderHelper.IsResolved<WearableDTO>(wearable));
        }

        [Test]
        public void NotTreatStoredPlaceholderWithCancelledDTOLoadAsResolved()
        {
            IWearable wearable = IWearable.NewEmpty();

            Assert.IsFalse(ElementProviderHelper.IsResolved<WearableDTO>(wearable));
        }

        [Test]
        public void NotTreatStoredPlaceholderWithFailedDTOLoadAsResolved()
        {
            IWearable wearable = IWearable.NewEmpty();

            ((IAvatarAttachment<WearableDTO>)wearable).ResolvedFailedDTO(
                new StreamableLoadingResult<WearableDTO>(ReportData.UNSPECIFIED, new ArgumentNullException("Wearable DTO is null")));

            Assert.IsFalse(ElementProviderHelper.IsResolved<WearableDTO>(wearable));
        }
    }
}
