using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
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
        private static readonly URLParameter WITH_REALMS_DETAIL = new ("with_realms_detail", "true");

        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;

        private readonly URLBuilder urlBuilder = new ();

        private string baseURL => decentralandUrlsSource.Url(DecentralandUrl.ApiPlaces);
        private URLDomain baseURLDomain => URLDomain.FromString(baseURL);
        private URLAddress poiURL => URLAddress.FromString(decentralandUrlsSource.Url(DecentralandUrl.POI));
        private URLAddress contentModerationReportURL => URLAddress.FromString(decentralandUrlsSource.Url(DecentralandUrl.ContentModerationReport));

        public PlacesAPIClient(IWebRequestController webRequestController, IDecentralandUrlsSource decentralandUrlsSource)
        {
            this.webRequestController = webRequestController;
            this.decentralandUrlsSource = decentralandUrlsSource;
        }

        private URLBuilder ResetURLBuilder()
        {
            urlBuilder.Clear();
            return urlBuilder;
        }

        public async UniTask<PlacesData.PlacesAPIResponse> SearchPlacesAsync(string searchString, int pageNumber, int pageSize, CancellationToken ct)
        {
            string url = baseURL + "?search={0}&offset={1}&limit={2}";

            GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> result = webRequestController.GetAsync(string.Format(url, searchString.Replace(" ", "+"), pageNumber * pageSize, pageSize), ct, ReportCategory.UI);

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
            string url = baseURL + "?order_by={3}&order=desc&with_realms_detail=true&offset={0}&limit={1}&{2}";

            GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> result = webRequestController.GetAsync(string.Format(url, pageNumber * pageSize, pageSize, filter, sort), ct, ReportCategory.UI);

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
            string url = baseURL + "?positions={0},{1}&with_realms_detail=true";

            GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> result = webRequestController.GetAsync(string.Format(url, coords.x, coords.y), ct, ReportCategory.UI);

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
            var url = $"{baseURL}/{placeUUID}?with_realms_detail=true";

            GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> result = webRequestController.GetAsync(url, ct, ReportCategory.UI);

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
               .AppendDomain(baseURLDomain);

            foreach (Vector2Int coords in coordsList)
                url.AppendParameter(("positions", $"{coords.x},{coords.y}")).AppendParameter(WITH_REALMS_DETAIL);

            GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> result = webRequestController.GetAsync(new CommonArguments(url.Build()), ct, ReportCategory.UI);

            using PoolExtensions.Scope<PlacesData.PlacesAPIResponse> rentedList = PlacesData.PLACES_API_RESPONSE_POOL.AutoScope();

            await result.OverwriteFromJsonAsync(rentedList.Value, WRJsonParser.Unity,
                             createCustomExceptionOnFailure: static (_, text) => new PlacesAPIException("Error parsing places info:", text))
                        .WithCustomExceptionAsync(static exc => new PlacesAPIException(exc, "Error fetching places info:"));

            targetList.AddRange(rentedList.Value.data);

            return targetList;
        }

        public async UniTask<PlacesData.IPlacesAPIResponse> GetFavoritesAsync(int pageNumber, int pageSize, CancellationToken ct)
        {
            string url = baseURL + "?only_favorites=true&with_realms_detail=true&offset={0}&limit={1}";

            GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> result = webRequestController.GetAsync(string.Format(url, pageNumber * pageSize, pageSize), ct, ReportCategory.UI);

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
            string url = baseURL + "?only_favorites=true&with_realms_detail=true";

            GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> result = webRequestController.GetAsync(url, ct, ReportCategory.UI);

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
            string url = baseURL + "/{0}/favorites";
            const string FAVORITE_PAYLOAD = "{\"favorites\": true}";
            const string NOT_FAVORITE_PAYLOAD = "{\"favorites\": false}";

            await webRequestController.PatchAsync(string.Format(url, placeUUID), GenericPatchArguments.CreateJson(isFavorite ? FAVORITE_PAYLOAD : NOT_FAVORITE_PAYLOAD), ct, ReportCategory.UI)
                                      .WithCustomExceptionAsync(static exc => new PlacesAPIException(exc, "Error setting place favorite:"));
        }

        public async UniTask SetPlaceVoteAsync(bool? isUpvote, string placeUUID, CancellationToken ct)
        {
            string url = baseURL + "/{0}/likes";
            const string LIKE_PAYLOAD = "{\"like\": true}";
            const string DISLIKE_PAYLOAD = "{\"like\": false}";
            const string NO_LIKE_PAYLOAD = "{\"like\": null}";

            string payload;

            if (isUpvote == null)
                payload = NO_LIKE_PAYLOAD;
            else
                payload = isUpvote == true ? LIKE_PAYLOAD : DISLIKE_PAYLOAD;

            await webRequestController.PostAsync(string.Format(url, placeUUID), GenericPostArguments.CreateJson(payload), ct, ReportCategory.UI)
                                      .WithCustomExceptionAsync(static exc => new PlacesAPIException(exc, "Error setting place vote:"));
        }

        public async UniTask<List<string>> GetPointsOfInterestCoordsAsync(CancellationToken ct)
        {
            GenericDownloadHandlerUtils.Adapter<GenericPostRequest, GenericPostArguments> result = webRequestController.PostAsync(poiURL, GenericPostArguments.Empty, ct, ReportCategory.UI);

            PointsOfInterestCoordsAPIResponse response = await result.CreateFromJson<PointsOfInterestCoordsAPIResponse>(WRJsonParser.Unity,
                createCustomExceptionOnFailure: static (_, text) => new PlacesAPIException("Error parsing get POIs response:", text));

            if (response.data == null)
                throw new Exception("No POIs info retrieved");

            return response.data;
        }

        public async UniTask ReportPlaceAsync(PlaceContentReportPayload placeContentReportPayload, CancellationToken ct)
        {
            // POST for getting a signed url
            GenericDownloadHandlerUtils.Adapter<GenericPostRequest, GenericPostArguments> postResult = webRequestController.PostAsync(contentModerationReportURL, GenericPostArguments.Empty, ct, ReportCategory.UI);

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

            await webRequestController.PutAsync(response.data.signed_url, GenericPutArguments.CreateJson(putData), ct, ReportCategory.UI)
                                      .WithCustomExceptionAsync(static exc => new PlacesAPIException(exc, "Error reporting place:"));
        }
    }
}
