using Cysharp.Threading.Tasks;
using DCL.PlacesAPIService;
using DCL.Prefs;
using NSubstitute;
using NSubstitute.Core;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DCL.Tests.Editor
{
    public class PlacesAPIServiceShould
    {
        private const string PLACE_ID = "genesis-plaza";
        private static readonly Vector2Int PARCEL = Vector2Int.zero;

        private IPlacesAPIClient client = null!;
        private PlacesAPIService.PlacesAPIService service = null!;
        private PlacesData.PlaceInfo place = null!;

        [SetUp]
        public async Task SetUp()
        {
            // The service constructor reads the recently visited places from the prefs, which nothing initializes in edit mode
            SetPrefs(new InMemoryDCLPlayerPrefs());

            place = new PlacesData.PlaceInfo(PARCEL) { id = PLACE_ID };

            client = Substitute.For<IPlacesAPIClient>();

            client.GetPlacesAsync(default)
                  .ReturnsForAnyArgs(UniTask.FromResult(new PlacesData.PlacesAPIResponse { ok = true, data = new List<PlacesData.PlaceInfo> { place } }));

            client.RatePlaceAsync(default, default!, default).ReturnsForAnyArgs(UniTask.CompletedTask);

            service = new PlacesAPIService.PlacesAPIService(client);

            // Warms the cache so the rating has an instance to land on
            await service.GetPlaceAsync(PARCEL, CancellationToken.None);
        }

        [TearDown]
        public void TearDown()
        {
            service.Dispose();
            SetPrefs(null);
        }

        [Test]
        public async Task ApplyLikeToCachedPlace()
        {
            await service.RatePlaceAsync(true, PLACE_ID, CancellationToken.None);

            PlacesData.PlaceInfo? cached = await service.GetPlaceAsync(PARCEL, CancellationToken.None);

            Assert.That(cached, Is.SameAs(place));
            Assert.That(cached!.user_like, Is.True);
            Assert.That(cached.user_dislike, Is.False);
        }

        [Test]
        public async Task ReplaceLikeWithDislikeOnCachedPlace()
        {
            await service.RatePlaceAsync(true, PLACE_ID, CancellationToken.None);
            await service.RatePlaceAsync(false, PLACE_ID, CancellationToken.None);

            Assert.That(place.user_like, Is.False);
            Assert.That(place.user_dislike, Is.True);
        }

        [Test]
        public async Task ClearRatingOnCachedPlace()
        {
            await service.RatePlaceAsync(true, PLACE_ID, CancellationToken.None);
            await service.RatePlaceAsync(null, PLACE_ID, CancellationToken.None);

            Assert.That(place.user_like, Is.False);
            Assert.That(place.user_dislike, Is.False);
        }

        [Test]
        public async Task RestoreCachedRatingWhenRequestFails()
        {
            await service.RatePlaceAsync(false, PLACE_ID, CancellationToken.None);

            client.RatePlaceAsync(default, default!, default).ReturnsForAnyArgs(UniTask.FromException(new Exception("rate failed")));

            try { await service.RatePlaceAsync(true, PLACE_ID, CancellationToken.None); }
            catch (Exception)
            {
                // the failing request is the scenario under test, only the restored rating matters
            }

            Assert.That(place.user_like, Is.False);
            Assert.That(place.user_dislike, Is.True);
        }

        [Test]
        public async Task ForwardOnlyHighlightedWhenSearchingDestinations()
        {
            // Arrange
            StubDestinations();

            // Act
            await service.SearchDestinationsAsync(0, 20, CancellationToken.None, onlyHighlighted: true);

            // Assert
            Assert.That(LastDestinationsArgument("onlyHighlighted"), Is.True);
        }

        [Test]
        public async Task LeaveOnlyHighlightedUnsetWhenSearchingDestinations()
        {
            // Arrange
            StubDestinations();

            // Act
            await service.SearchDestinationsAsync(0, 20, CancellationToken.None);

            // Assert
            Assert.That(LastDestinationsArgument("onlyHighlighted"), Is.Null);
        }

        [TestCase("featured", "", true)]
        [TestCase("featured", "plaza", false)]
        [TestCase("social", "", false)]
        [TestCase(null, "", false)]
        public void BrowseFeaturedOnlyWithoutSearch(string? categoryId, string searchText, bool expected)
        {
            // Arrange
            var filters = new DCL.Places.PlacesFilters { CategoryId = categoryId, SearchText = searchText };

            // Act
            bool isBrowsingFeatured = filters.IsBrowsingFeatured;

            // Assert
            Assert.That(isBrowsingFeatured, Is.EqualTo(expected));
        }

        private void StubDestinations() =>
            client.GetDestinationsAsync(default)
                  .ReturnsForAnyArgs(UniTask.FromResult(new PlacesData.PlacesAPIResponse { ok = true, data = new List<PlacesData.PlaceInfo>() }));

        // Reads the argument by parameter name so the assertion survives reordering of the client's many optional parameters.
        private object? LastDestinationsArgument(string parameterName)
        {
            ICall? call = null;

            foreach (ICall received in client.ReceivedCalls())
                if (received.GetMethodInfo().Name == nameof(IPlacesAPIClient.GetDestinationsAsync))
                    call = received;

            Assert.That(call, Is.Not.Null, $"{nameof(IPlacesAPIClient.GetDestinationsAsync)} was never called");

            ParameterInfo[] parameters = call!.GetMethodInfo().GetParameters();

            for (var i = 0; i < parameters.Length; i++)
                if (parameters[i].Name == parameterName)
                    return call.GetArguments()[i];

            throw new ArgumentException($"{nameof(IPlacesAPIClient.GetDestinationsAsync)} has no parameter named {parameterName}");
        }

        private static void SetPrefs(IDCLPrefs? prefs)
        {
            FieldInfo field = typeof(DCLPlayerPrefs).GetField("dclPrefs", BindingFlags.NonPublic | BindingFlags.Static)!;
            field.SetValue(null, prefs);
        }
    }
}
