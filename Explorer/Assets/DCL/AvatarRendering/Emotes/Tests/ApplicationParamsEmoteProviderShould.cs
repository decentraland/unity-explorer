using ECS.StreamableLoading.Common.Components;
using Global.AppArgs;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DCL.AvatarRendering.Emotes.Tests
{
    [TestFixture]
    public class ApplicationParamsEmoteProviderShould
    {
        private const string COLLECTION_ID = "1a2b3c4d-5e6f-4a8b-9c0d-1e2f3a4b5c6d";
        private const string BUILDER_DTOS_URL = "https://builder-api.decentraland.zone/v1/collections/[COL-ID]/items";
        private const string EXPECTED_URL = "https://builder-api.decentraland.zone/v1/collections/" + COLLECTION_ID + "/items";

        private IAppArgs appArgs = null!;
        private IEmoteProvider source = null!;
        private ApplicationParamsEmoteProvider provider = null!;

        [SetUp]
        public void SetUp()
        {
            appArgs = Substitute.For<IAppArgs>();
            source = Substitute.For<IEmoteProvider>();
            provider = new ApplicationParamsEmoteProvider(appArgs, source, BUILDER_DTOS_URL);
        }

        [Test]
        public async Task RequestBuilderCollectionWhenIdIsAGuid()
        {
            // Arrange
            GivenBuilderCollections(COLLECTION_ID);

            // Act
            await provider.GetTrimmedByParamsAsync(default(IEmoteProvider.OwnedEmotesRequestOptions), CancellationToken.None);

            // Assert
            _ = source.Received(1)
                      .GetTrimmedByParamsAsync(Arg.Any<IEmoteProvider.OwnedEmotesRequestOptions>(), Arg.Any<CancellationToken>(),
                          Arg.Any<List<ITrimmedEmote>?>(),
                          Arg.Is<CommonLoadingArguments?>(args => args.HasValue && args.Value.URL.Value == EXPECTED_URL),
                          true);
        }

        [TestCase("../../lands")]
        [TestCase("collections?id=1")]
        [TestCase(COLLECTION_ID + "#/items")]
        [TestCase("not-a-guid")]
        public async Task SkipBuilderCollectionWhenIdIsNotAGuid(string collectionId)
        {
            // Arrange
            GivenBuilderCollections(collectionId);

            // Act
            await provider.GetTrimmedByParamsAsync(default(IEmoteProvider.OwnedEmotesRequestOptions), CancellationToken.None);

            // Assert
            _ = source.DidNotReceive()
                      .GetTrimmedByParamsAsync(Arg.Any<IEmoteProvider.OwnedEmotesRequestOptions>(), Arg.Any<CancellationToken>(),
                          Arg.Any<List<ITrimmedEmote>?>(), Arg.Any<CommonLoadingArguments?>(), Arg.Any<bool>());
        }

        [Test]
        public async Task RequestOnlyTheValidCollectionsOfTheList()
        {
            // Arrange
            GivenBuilderCollections($"../../lands,{COLLECTION_ID},collections?id=1");

            // Act
            await provider.GetTrimmedByParamsAsync(default(IEmoteProvider.OwnedEmotesRequestOptions), CancellationToken.None);

            // Assert
            _ = source.Received(1)
                      .GetTrimmedByParamsAsync(Arg.Any<IEmoteProvider.OwnedEmotesRequestOptions>(), Arg.Any<CancellationToken>(),
                          Arg.Any<List<ITrimmedEmote>?>(), Arg.Any<CommonLoadingArguments?>(), true);
        }

        private void GivenBuilderCollections(string collectionsCsv)
        {
            appArgs.TryGetValue(AppArgsFlags.SELF_PREVIEW_BUILDER_COLLECTIONS, out Arg.Any<string?>())
                   .Returns(call =>
                    {
                        call[1] = collectionsCsv;
                        return true;
                    });
        }
    }
}
