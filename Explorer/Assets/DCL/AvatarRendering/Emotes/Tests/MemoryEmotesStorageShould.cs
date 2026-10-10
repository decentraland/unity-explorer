using CommunicationData.URLHelpers;
using DCL.Optimization.PerformanceBudgeting;
using NSubstitute;
using NUnit.Framework;

namespace DCL.AvatarRendering.Emotes.Tests
{
    public class MemoryEmotesStorageShould
    {
        private const string EMOTE_URN = "urn:decentraland:off-chain:base-emotes:dance";

        private MemoryEmotesStorage storage = null!;
        private IPerformanceBudget budget = null!;

        [SetUp]
        public void SetUp()
        {
            storage = new MemoryEmotesStorage();
            budget = Substitute.For<IPerformanceBudget>();
            budget.TrySpendBudget().Returns(true);
        }

        /// <summary>
        /// Regression for https://github.com/decentraland/unity-explorer/issues/9485: evicting an emote whose
        /// load is in flight orphans the pending promise and strands the play intent waiting on it.
        /// </summary>
        [Test]
        public void KeepLoadingEmoteResidentDuringUnload()
        {
            IEmote emote = storage.GetOrAddByDTO(NewDto());
            emote.UpdateLoadingStatus(true);

            storage.Unload(budget);

            Assert.IsTrue(storage.TryGetElement(new URN(EMOTE_URN), out _),
                "An emote with a load in flight must survive the memory sweep.");
        }

        [Test]
        public void EvictIdleEmoteWithoutAssetsDuringUnload()
        {
            storage.GetOrAddByDTO(NewDto());

            storage.Unload(budget);

            Assert.IsFalse(storage.TryGetElement(new URN(EMOTE_URN), out _),
                "An idle emote holding no assets is reclaimable by the memory sweep.");
        }

        private static EmoteDTO NewDto() =>
            new ()
            {
                metadata = new EmoteDTO.EmoteMetadataDto { id = EMOTE_URN },
            };
    }
}
