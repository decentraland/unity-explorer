using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables.Components;
using ECS.StreamableLoading.Common.Components;
using Global.AppArgs;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DCL.AvatarRendering.Wearables.Tests
{
    [TestFixture]
    public class ApplicationParametersWearablesProviderShould
    {
        private const string COLLECTION_ID = "1a2b3c4d-5e6f-4a8b-9c0d-1e2f3a4b5c6d";
        private const string BUILDER_DTOS_URL = "https://builder-api.decentraland.zone/v1/collections/[COL-ID]/items";
        private const string EXPECTED_URL = "https://builder-api.decentraland.zone/v1/collections/" + COLLECTION_ID + "/items";

        private IAppArgs appArgs = null!;
        private IWearablesProvider source = null!;
        private ApplicationParametersWearablesProvider provider = null!;

        [SetUp]
        public void SetUp()
        {
            appArgs = Substitute.For<IAppArgs>();
            source = Substitute.For<IWearablesProvider>();

            source.GetTrimmedByParamsAsync(Arg.Any<IWearablesProvider.Params>(), Arg.Any<CancellationToken>(),
                       Arg.Any<List<ITrimmedWearable>?>(), Arg.Any<CommonLoadingArguments?>(), Arg.Any<bool>())
                  .Returns(UniTask.FromResult<(IReadOnlyList<ITrimmedWearable> results, int totalAmount)>((Array.Empty<ITrimmedWearable>(), 0)));

            provider = new ApplicationParametersWearablesProvider(appArgs, source, BUILDER_DTOS_URL);
        }

        [Test]
        public async Task RequestBuilderCollectionWhenIdIsAGuid()
        {
            // Arrange
            GivenBuilderCollections(COLLECTION_ID);

            // Act
            await provider.GetTrimmedByParamsAsync(new IWearablesProvider.Params(10, 1), CancellationToken.None);

            // Assert
            _ = source.Received(1)
                      .GetTrimmedByParamsAsync(Arg.Any<IWearablesProvider.Params>(), Arg.Any<CancellationToken>(),
                          Arg.Any<List<ITrimmedWearable>?>(),
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
            await provider.GetTrimmedByParamsAsync(new IWearablesProvider.Params(10, 1), CancellationToken.None);

            // Assert
            _ = source.DidNotReceive()
                      .GetTrimmedByParamsAsync(Arg.Any<IWearablesProvider.Params>(), Arg.Any<CancellationToken>(),
                          Arg.Any<List<ITrimmedWearable>?>(), Arg.Any<CommonLoadingArguments?>(), true);
        }

        [Test]
        public async Task RequestOnlyTheValidCollectionsOfTheList()
        {
            // Arrange
            GivenBuilderCollections($"../../lands,{COLLECTION_ID},collections?id=1");

            // Act
            await provider.GetTrimmedByParamsAsync(new IWearablesProvider.Params(10, 1), CancellationToken.None);

            // Assert
            _ = source.Received(1)
                      .GetTrimmedByParamsAsync(Arg.Any<IWearablesProvider.Params>(), Arg.Any<CancellationToken>(),
                          Arg.Any<List<ITrimmedWearable>?>(), Arg.Any<CommonLoadingArguments?>(), true);
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
