using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using ECS.StreamableLoading.Common.Components;
using NUnit.Framework;

namespace DCL.AvatarRendering.Wearables.Tests
{
    public class WearableShould
    {
        [TestCase("urn:decentraland:matic:collections-v2:0x84a1d84f183fa0fd9b6b9cb1ed0ff1b7f5409ebb:5", "upper_body")]
        [TestCase("urn:decentraland:matic:collections-v2:0x05a4b4edfe92548cf11b6532e951dbb028922e5c:0:185", "hat")]
        public void BeOnChain(string urn, string category)
        {
            var model = new WearableDTO
            {
                metadata = new WearableDTO.WearableMetadataDto
                {
                    id = urn,
                    data = new WearableDTO.WearableMetadataDto.DataDto
                    {
                        category = category,
                    },
                },
            };

            Wearable wearable = new Wearable(new StreamableLoadingResult<WearableDTO>(model));

            Assert.IsTrue(wearable.IsOnChain());
        }

        [TestCase("urn:decentraland:off-chain:base-avatars:aviatorstyle", "hat")]
        [TestCase("urn:decentraland:off-chain:base-avatars:baggy_pullover", "upper_body")]
        public void BeOffChain(string urn, string category)
        {
            var model = new WearableDTO
            {
                metadata = new WearableDTO.WearableMetadataDto
                {
                    id = urn,
                    data = new WearableDTO.WearableMetadataDto.DataDto
                    {
                        category = category,
                    },
                },
            };

            Wearable wearable = new Wearable(new StreamableLoadingResult<WearableDTO>(model));

            Assert.IsFalse(wearable.IsOnChain());
        }
    }
}
