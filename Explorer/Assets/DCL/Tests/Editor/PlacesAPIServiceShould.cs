using Cysharp.Threading.Tasks;
using DCL.PlacesAPIService;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
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
            place = new PlacesData.PlaceInfo(PARCEL) { id = PLACE_ID };

            client = Substitute.For<IPlacesAPIClient>();

            client.GetPlacesAsync(default)
                  .ReturnsForAnyArgs(UniTask.FromResult(new PlacesData.PlacesAPIResponse { ok = true, data = new List<PlacesData.PlaceInfo> { place } }));

            client.RatePlaceAsync(default, default!, default).ReturnsForAnyArgs(UniTask.CompletedTask);

            service = new PlacesAPIService.PlacesAPIService(client);

            // Warms the cache so the rating has an instance to land on
            await service.GetPlaceAsync(PARCEL, CancellationToken.None);
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
            catch (Exception) { }

            Assert.That(place.user_like, Is.False);
            Assert.That(place.user_dislike, Is.True);
        }
    }
}
