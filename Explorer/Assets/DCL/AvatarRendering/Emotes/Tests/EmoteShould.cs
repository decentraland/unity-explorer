using ECS.StreamableLoading.Common.Components;
using NUnit.Framework;

namespace DCL.AvatarRendering.Emotes.Tests
{
    public class EmoteShould
    {
        [TestCase("urn:decentraland:matic:collections-v2:0x0ae365f8acc27f2c95fc7d60cf49a74f3af21573:3:315936875005671560093754083051011296956685286201647333762932932939")]
        [TestCase("urn:decentraland:matic:collections-v2:0xded1e53d7a43ac1844b66c0ca0f02627eb42e16d:8")]
        public void BeOnChain(string urn)
        {
            var model = new EmoteDTO
            {
                metadata = new EmoteDTO.Metadata
                {
                    id = urn,
                },
            };

            Emote emote = new Emote(new StreamableLoadingResult<EmoteDTO>(model));

            Assert.IsTrue(emote.IsOnChain());
        }

        [TestCase("clap")]
        [TestCase("urn:decentraland:off-chain:base-avatars:dance")]
        public void BeOffChain(string urn)
        {
            var model = new EmoteDTO
            {
                metadata = new EmoteDTO.Metadata
                {
                    id = urn,
                },
            };

            Emote emote = new Emote(new StreamableLoadingResult<EmoteDTO>(model));

            Assert.IsFalse(emote.IsOnChain());
        }
    }
}
