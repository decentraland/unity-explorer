using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Emotes.Load;
using DCL.AvatarRendering.Loading.Components;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Emotes.Tests
{
    public class LoadEmotesByPointersSystemShould : UnitySystemTestBase<LoadEmotesByPointersSystem>
    {
        private const string CLAP = "urn:decentraland:off-chain:base-emotes:clap";
        private const string CONFETTI = "urn:decentraland:off-chain:base-emotes:confettipopper";
        private MemoryEmotesStorage storage = null!;

        [SetUp]
        public void SetUp()
        {
            storage = new MemoryEmotesStorage();
            var urls = Substitute.For<IDecentralandUrlsSource>();
            urls.Url(DecentralandUrl.EntitiesActiveElements).Returns("https://registry.example/entities/active");
            system = new LoadEmotesByPointersSystem(world, IWebRequestController.TEST,
                new NoCache<EmotesDTOList, GetEmotesDTOByPointersFromRealmIntention>(false, false),
                storage, urls, default,
                new EntitiesAnalytics(IAnalyticsController.Null, new EntitiesAnalyticsDebug(null)), false);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ResolveCatalogMetadataWhilePlaybackIsLoading(bool metadataOnly)
        {
            var pointers = new List<URN> { CLAP, CONFETTI };
            foreach (URN urn in pointers)
                storage.Set(urn, EmoteWithMetadata(urn));

            using var intention = EmoteComponentsUtils.CreateGetEmotesByPointersIntention(BodyShape.MALE, pointers, metadataOnly);
            var promise = AssetPromise<EmotesResolution, GetEmotesByPointersIntention>.Create(world, intention, PartitionComponent.TOP_PRIORITY);
            system.Update(0);

            Assert.That(promise.TryGetResult(world, out var response), Is.EqualTo(metadataOnly));
            if (metadataOnly)
            {
                using var emotes = response.Asset.ConsumeEmotes();
                Assert.That(emotes.Value, Has.Count.EqualTo(2));
                Assert.That(emotes.Value[0].GetUrn(), Is.EqualTo(pointers[0]));
                Assert.That(emotes.Value[1].AssetResults[BodyShape.MALE], Is.Null);
                Assert.That(emotes.Value[1].IsLoading, Is.True);
            }
        }

        [Test]
        public void ReturnAvailableCatalogMetadataWhenAnotherPointerTimesOut()
        {
            var clap = EmoteWithMetadata(CLAP);
            var pending = new Emote();
            pending.UpdateLoadingStatus(true);
            storage.Set(CLAP, clap);
            storage.Set(CONFETTI, pending);

            using var intention = EmoteComponentsUtils.CreateGetEmotesByPointersIntention(default, new URN[] { CLAP, CONFETTI }, metadataOnly: true);
            var promise = AssetPromise<EmotesResolution, GetEmotesByPointersIntention>.Create(world, intention, PartitionComponent.TOP_PRIORITY);
            system.Update(0);
            Assert.That(promise.TryGetResult(world, out _), Is.False);

            system.Update(61);
            Assert.That(promise.TryGetResult(world, out var response), Is.True);
            using var emotes = response.Asset.ConsumeEmotes();
            Assert.That(emotes.Value, Is.EqualTo(new[] { clap }));
            Assert.That(pending.IsLoading, Is.False);
        }

        [Test]
        public void RetryAFailedMetadataPointerOncePerCatalogRequest()
        {
            var failure = new StreamableLoadingResult<EmoteDTO>(new ReportData(ReportCategory.EMOTE), new Exception("Catalog unavailable"));
            IEmote failed = new Emote { Model = failure };
            storage.Set(CONFETTI, failed);
            using var intention = EmoteComponentsUtils.CreateGetEmotesByPointersIntention(default, new URN[] { CONFETTI }, metadataOnly: true);
            var promise = AssetPromise<EmotesResolution, GetEmotesByPointersIntention>.Create(world, intention, PartitionComponent.TOP_PRIORITY);

            system.Update(0);
            Assert.That(failed.IsLoading, Is.True);
            Assert.That(promise.TryGetResult(world, out _), Is.False);
            failed.ResolvedFailedDTO(failure);
            system.Update(0);

            Assert.That(promise.TryGetResult(world, out var response), Is.True);
            using var emotes = response.Asset.ConsumeEmotes();
            Assert.That(emotes.Value, Is.Empty);
        }

        private static Emote EmoteWithMetadata(URN urn) =>
            new (new StreamableLoadingResult<EmoteDTO>(new EmoteDTO
            {
                metadata = new EmoteDTO.EmoteMetadataDto { id = urn },
            }), isLoading: true);

        [Test]
        public void RemoveTheEmotesWhoseDTOFailedFromTheResult()
        {
            // Arrange
            var resolved = new Emote(new StreamableLoadingResult<EmoteDTO>(new EmoteDTO
            {
                metadata = new EmoteDTO.EmoteMetadataDto { id = "urn:decentraland:off-chain:base-emotes:wave" },
            }));

            var failed = new Emote
            {
                Model = new StreamableLoadingResult<EmoteDTO>(new ReportData(ReportCategory.EMOTE), new Exception("Emote DTO is null")),
            };

            var emotes = new List<IEmote> { failed, resolved };

            // Act
            LoadEmotesByPointersSystem.RemoveEmotesWithFailedDTO(emotes);

            // Assert
            Assert.That(emotes, Is.EquivalentTo(new IEmote[] { resolved }));
        }
    }
}
