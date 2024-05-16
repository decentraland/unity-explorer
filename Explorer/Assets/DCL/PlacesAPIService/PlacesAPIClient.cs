using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Optimization.Pools;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.PlacesAPIService
{
    public class PlacesAPIClient : IPlacesAPIClient
    {
        private const string BASE_URL = "https://places.decentraland.org/api/places";
        private const string BASE_URL_ZONE = "https://places.decentraland.zone/api/places";

        private static readonly URLAddress POI_URL = URLAddress.FromString("https://dcl-lists.decentraland.org/pois");
        private static readonly URLAddress CONTENT_MODERATION_REPORT_URL = URLAddress.FromString("https://places.decentraland.org/api/report");

        private static readonly URLDomain BASE_URL_DOMAIN = URLDomain.FromString(BASE_URL);
        private static readonly URLParameter WITH_REALMS_DETAIL = new ("with_realms_detail", "true");

        private readonly IWebRequestController webRequestController;

        private readonly URLBuilder urlBuilder = new ();

        public PlacesAPIClient(IWebRequestController webRequestController)
        {
            this.webRequestController = webRequestController;
        }

        private URLBuilder ResetURLBuilder()
        {
            urlBuilder.Clear();
            return urlBuilder;
        }

        public async UniTask<PlacesData.PlacesAPIResponse> SearchPlacesAsync(string searchString, int pageNumber, int pageSize, CancellationToken ct)
        {
            const string URL = BASE_URL + "?search={0}&offset={1}&limit={2}";

            var result = webRequestController.GetAsync(string.Format(URL, searchString.Replace(" ", "+"), pageNumber * pageSize, pageSize), ct);

            PlacesData.PlacesAPIResponse response = PlacesData.PLACES_API_RESPONSE_POOL.Get();

            await result.OverwriteFromJsonAsync(response, WRJsonParser.Unity,
                createCustomExceptionOnFailure: static (_, text) => new PlacesAPIException("Error parsing search places info:", text))
                .WithCustomExceptionAsync(static exc => new PlacesAPIException(exc, "Error fetching search places info:"));

            if (response.data == null)
                throw new PlacesAPIException($"No place info retrieved:\n{searchString}");

            return response;
        }

        public async UniTask<PlacesData.PlacesAPIResponse> GetMostActivePlacesAsync(int pageNumber, int pageSize, string filter = "", string sort = "", CancellationToken ct = default)
        {
            const string URL = BASE_URL + "?order_by={3}&order=desc&with_realms_detail=true&offset={0}&limit={1}&{2}";

            var result = webRequestController.GetAsync(string.Format(URL, pageNumber * pageSize, pageSize, filter, sort), ct);

            PlacesData.PlacesAPIResponse response = PlacesData.PLACES_API_RESPONSE_POOL.Get();

            await result.OverwriteFromJsonAsync(response, WRJsonParser.Unity,
                createCustomExceptionOnFailure: static (_, text) => new PlacesAPIException("Error parsing most active places info:", text))
                        .WithCustomExceptionAsync(static exc => new PlacesAPIException(exc, "Error fetching most active places info:"));

            if (response.data == null)
                throw new PlacesAPIException("No place info retrieved");

            return response;
        }

        public async UniTask<PlacesData.PlaceInfo?> GetPlaceAsync(Vector2Int coords, CancellationToken ct)
        {
            const string URL = BASE_URL + "?positions={0},{1}&with_realms_detail=true";

            var result = webRequestController.GetAsync(string.Format(URL, coords.x, coords.y), ct);

            using PlacesData.PlacesAPIResponse response = PlacesData.PLACES_API_RESPONSE_POOL.Get();

            await result.OverwriteFromJsonAsync(response, WRJsonParser.Unity,
                createCustomExceptionOnFailure: static (_, text) => new PlacesAPIException("Error parsing place info:", text))
                        .WithCustomExceptionAsync(static exc => new PlacesAPIException(exc, "Error fetching place info:"));

            if (response.data.Count == 0)
                return null;

            return response.data[0];
        }

        public async UniTask<PlacesData.PlaceInfo?> GetPlaceAsync(string placeUUID, CancellationToken ct)
        {
            var url = $"{BASE_URL}/{placeUUID}?with_realms_detail=true";

            var result = webRequestController.GetAsync(url, ct);

            PlacesData.PlacesAPIGetParcelResponse response = await result.CreateFromJson<PlacesData.PlacesAPIGetParcelResponse>(WRJsonParser.Unity,
                createCustomExceptionOnFailure: static (_, text) => new PlacesAPIException("Error parsing place info:", text))
                .WithCustomExceptionAsync(static exc => new PlacesAPIException(exc, "Error fetching place info:"));

            if (!response.ok)
                throw new NotAPlaceException(placeUUID);

            // At this moment WR is already disposed
            return response.data;
        }

        public async UniTask<List<PlacesData.PlaceInfo>> GetPlacesByCoordsListAsync(IReadOnlyList<Vector2Int> coordsList, List<PlacesData.PlaceInfo> targetList, CancellationToken ct)
        {
            targetList.Clear();

            if (coordsList.Count == 0)
                return targetList;

            IURLBuilder url = ResetURLBuilder()
               .AppendDomain(BASE_URL_DOMAIN);

            foreach (Vector2Int coords in coordsList)
                url.AppendParameter(("positions", $"{coords.x},{coords.y}")).AppendParameter(WITH_REALMS_DETAIL);

            var result = webRequestController.GetAsync(new CommonArguments(url.Build()), ct);

            using PoolExtensions.Scope<PlacesData.PlacesAPIResponse> rentedList = PlacesData.PLACES_API_RESPONSE_POOL.AutoScope();

            await result.OverwriteFromJsonAsync(rentedList.Value, WRJsonParser.Unity,
                createCustomExceptionOnFailure: static (_, text) => new PlacesAPIException("Error parsing places info:", text))
                        .WithCustomExceptionAsync(static exc => new PlacesAPIException(exc, "Error fetching places info:"));

            targetList.AddRange(rentedList.Value.data);

            return targetList;
        }

        public async UniTask<PlacesData.IPlacesAPIResponse> GetFavoritesAsync(int pageNumber, int pageSize, CancellationToken ct)
        {
            const string URL = BASE_URL + "?only_favorites=true&with_realms_detail=true&offset={0}&limit={1}";

            var result = webRequestController.GetAsync(string.Format(URL, pageNumber * pageSize, pageSize), ct);

            PlacesData.PlacesAPIResponse response = PlacesData.PLACES_API_RESPONSE_POOL.Get();

            await result.OverwriteFromJsonAsync(response, WRJsonParser.Unity,
                createCustomExceptionOnFailure: static (_, text) => new PlacesAPIException("Error parsing get favorites response:", text))
                        .WithCustomExceptionAsync(static exc => new PlacesAPIException(exc, "Error fetching places info:"));

            if (response.data == null)
                throw new Exception("No favorites info retrieved");

            return response;
        }

        public async UniTask<PlacesData.IPlacesAPIResponse> GetAllFavoritesAsync(CancellationToken ct)
        {
            const string URL = BASE_URL + "?only_favorites=true&with_realms_detail=true";

            var result = webRequestController.GetAsync(URL, ct);

            PlacesData.PlacesAPIResponse response = PlacesData.PLACES_API_RESPONSE_POOL.Get();

            await result.OverwriteFromJsonAsync(response, WRJsonParser.Unity,
                createCustomExceptionOnFailure: static (_, text) => new PlacesAPIException("Error parsing favorites info:", text))
                        .WithCustomExceptionAsync(static exc => new PlacesAPIException(exc, "Error fetching favorites info:"));

            if (response.data == null)
                throw new PlacesAPIException($"No favorites info retrieved");

            return response;
        }

        public async UniTask SetPlaceFavoriteAsync(string placeUUID, bool isFavorite, CancellationToken ct)
        {
            const string URL = BASE_URL + "/{0}/favorites";
            const string FAVORITE_PAYLOAD = "{\"favorites\": true}";
            const string NOT_FAVORITE_PAYLOAD = "{\"favorites\": false}";

            await webRequestController.PatchAsync(string.Format(URL, placeUUID), GenericPatchArguments.CreateJson(isFavorite ? FAVORITE_PAYLOAD : NOT_FAVORITE_PAYLOAD), ct)
                                      .WithCustomExceptionAsync(static exc => new PlacesAPIException(exc, "Error setting place favorite:"));
        }

        public async UniTask SetPlaceVoteAsync(bool? isUpvote, string placeUUID, CancellationToken ct)
        {
            const string URL = BASE_URL + "/{0}/likes";
            const string LIKE_PAYLOAD = "{\"like\": true}";
            const string DISLIKE_PAYLOAD = "{\"like\": false}";
            const string NO_LIKE_PAYLOAD = "{\"like\": null}";

            string payload;

            if (isUpvote == null)
                payload = NO_LIKE_PAYLOAD;
            else
                payload = isUpvote == true ? LIKE_PAYLOAD : DISLIKE_PAYLOAD;

            await webRequestController.PostAsync(string.Format(URL, placeUUID), GenericPostArguments.CreateJson(payload), ct)
                                      .WithCustomExceptionAsync(static exc => new PlacesAPIException(exc, "Error setting place vote:"));
        }

        public async UniTask<List<string>> GetPointsOfInterestCoordsAsync(CancellationToken ct)
        {
            var result = webRequestController.PostAsync(POI_URL, GenericPostArguments.Empty, ct);

            PointsOfInterestCoordsAPIResponse response = await result.CreateFromJson<PointsOfInterestCoordsAPIResponse>(WRJsonParser.Unity,
                createCustomExceptionOnFailure: static (_, text) => new PlacesAPIException("Error parsing get POIs response:", text));

            if (response.data == null)
                throw new Exception("No POIs info retrieved");

            return response.data;
        }

        public async UniTask ReportPlaceAsync(PlaceContentReportPayload placeContentReportPayload, CancellationToken ct)
        {
            // POST for getting a signed url
            var postResult = webRequestController.PostAsync(CONTENT_MODERATION_REPORT_URL, GenericPostArguments.Empty, ct);

            using PoolExtensions.Scope<ReportPlaceAPIResponse> responseRental = PlacesData.REPORT_PLACE_API_RESPONSE_POOL.AutoScope();
            ReportPlaceAPIResponse response = responseRental.Value;

            await postResult.OverwriteFromJsonAsync(response, WRJsonParser.Unity,
                WRThreadFlags.SwitchToThreadPool, // don't return to the main thread
                createCustomExceptionOnFailure: static (_, text) => new PlacesAPIException("Error parsing report place response:", text))
                .WithCustomExceptionAsync(static exc => new PlacesAPIException(exc, "Error reporting place:"));

            if (response.data == null || !response.ok)
                throw new PlacesAPIException("Error reporting place");

            // PUT using the gotten signed url and sending the report payload

            string putData = JsonUtility.ToJson(placeContentReportPayload);

            await UniTask.SwitchToMainThread();

            await webRequestController.PutAsync(response.data.signed_url, GenericPutArguments.CreateJson(putData), ct)
                                      .WithCustomExceptionAsync(static exc => new PlacesAPIException(exc, "Error reporting place:"));
        }
    }
}
