using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Optimization.Pools;
using DCL.PlacesAPIService.Serialization;
using DCL.WebRequests;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility.Times;

namespace DCL.PlacesAPIService
{
    public class PlacesAPIClient : IPlacesAPIClient
    {
        private static readonly JsonSerializerSettings SERIALIZER_SETTINGS = new () { Converters = new JsonConverter[] { new PlacesByCategoryJsonDtoConverter() } };
        private static readonly URLParameter WITH_REALMS_DETAIL = new ("with_realms_detail", "true");
        private static readonly URLParameter ONLY_FAVORITES = new ("only_favorites", "true");

        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;

        private readonly URLBuilder urlBuilder = new ();

        private string baseURL => decentralandUrlsSource.Url(DecentralandUrl.ApiPlaces);
        private URLDomain baseURLDomain => URLDomain.FromString(baseURL);
        private URLAddress poiURL => URLAddress.FromString(decentralandUrlsSource.Url(DecentralandUrl.POI));
        private URLAddress mapApiUrl => URLAddress.FromString(decentralandUrlsSource.Url(DecentralandUrl.Map));
        private URLAddress contentModerationReportURL => URLAddress.FromString(decentralandUrlsSource.Url(DecentralandUrl.ContentModerationReport));

        public PlacesAPIClient(IWebRequestController webRequestController, IDecentralandUrlsSource decentralandUrlsSource)
        {
            this.webRequestController = webRequestController;
            this.decentralandUrlsSource = decentralandUrlsSource;
        }

        public async UniTask<PlacesData.PlacesAPIResponse> GetPlacesAsync(CancellationToken ct,
            string? searchString = null,
            (int pageNumber, int pageSize)? pagination = null,
            string? sortBy = null, string? sortDirection = null,
            string? category = null,
            bool? onlyFavorites = null,
            bool? addRealmDetails = null,
            IReadOnlyList<string>? positions = null,
            List<PlacesData.PlaceInfo>? resultBuffer = null)
        {
            urlBuilder.Clear();
            urlBuilder.AppendDomain(URLDomain.FromString(baseURL));

            if (!string.IsNullOrEmpty(searchString))
                urlBuilder.AppendParameter(new URLParameter("search", searchString.Replace(" ", "+")));

            if (pagination != null)
            {
                urlBuilder.AppendParameter(new URLParameter("offset", (pagination?.pageNumber * pagination?.pageSize).ToString()!));
                urlBuilder.AppendParameter(new URLParameter("limit", pagination?.pageSize.ToString()!));
            }

            if (!string.IsNullOrEmpty(sortBy))
                urlBuilder.AppendParameter(new URLParameter("order_by", sortBy));

            if (!string.IsNullOrEmpty(sortDirection))
                urlBuilder.AppendParameter(new URLParameter("order", sortDirection));

            if (!string.IsNullOrEmpty(category))
                urlBuilder.AppendParameter(new URLParameter("category", category));

            if (onlyFavorites != null)
                urlBuilder.AppendParameter(ONLY_FAVORITES);

            if (addRealmDetails != null)
                urlBuilder.AppendParameter(WITH_REALMS_DETAIL);

            if (positions != null)
                foreach (string xy in positions)
                    urlBuilder.AppendParameter(new URLParameter("positions", xy));

            URLAddress url = urlBuilder.Build();

            ulong timestamp = DateTime.UtcNow.UnixTimeAsMilliseconds();

            GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> result = webRequestController.GetAsync(
                url, ct,
                ReportCategory.UI,
                signInfo: WebRequestSignInfo.NewFromUrl(url, timestamp, "get"),
                headersInfo: new WebRequestHeadersInfo().WithSign(string.Empty, timestamp));

            PlacesData.PlacesAPIResponse response = PlacesData.PLACES_API_RESPONSE_POOL.Get();

            await result.OverwriteFromJsonAsync(response, WRJsonParser.Unity,
                             createCustomExceptionOnFailure: static (_, text) => new PlacesAPIException("Error parsing search places info:", text))
                        .WithCustomExceptionAsync(static exc => new PlacesAPIException(exc, "Error fetching search places info:"));

            if (response.data == null)
                throw new PlacesAPIException($"No place info retrieved:\n{searchString}");

            resultBuffer?.AddRange(response.data);

            return response;
        }

        public async UniTask<PlacesData.PlaceInfo?> GetPlaceAsync(string placeId, CancellationToken ct)
        {
            ulong timestamp = DateTime.UtcNow.UnixTimeAsMilliseconds();
            var url = $"{baseURL}/{placeId}?with_realms_detail=true";

            GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> result = webRequestController.GetAsync(
                url, ct, ReportCategory.UI,
                signInfo: WebRequestSignInfo.NewFromUrl(url, timestamp, "get"),
                headersInfo: new WebRequestHeadersInfo().WithSign(string.Empty, timestamp));

            PlacesData.PlacesAPIGetParcelResponse response = await result.CreateFromJson<PlacesData.PlacesAPIGetParcelResponse>(WRJsonParser.Unity,
                                                                              createCustomExceptionOnFailure: static (_, text) => new PlacesAPIException("Error parsing place info:", text))
                                                                         .WithCustomExceptionAsync(static exc => new PlacesAPIException(exc, "Error fetching place info:"));

            if (!response.ok)
                throw new NotAPlaceException(placeId);

            // At this moment WR is already disposed
            return response.data;
        }

        public async UniTask SetPlaceFavoriteAsync(string placeId, bool isFavorite, CancellationToken ct)
        {
            ulong unixTimestamp = DateTime.UtcNow.UnixTimeAsMilliseconds();
            string url = baseURL + "/{0}/favorites";
            const string FAVORITE_PAYLOAD = "{\"favorites\": true}";
            const string NOT_FAVORITE_PAYLOAD = "{\"favorites\": false}";

            var fullUrl = string.Format(url, placeId);

            await webRequestController.PatchAsync(
                                           fullUrl,
                                           GenericPatchArguments.CreateJson(isFavorite ? FAVORITE_PAYLOAD : NOT_FAVORITE_PAYLOAD),
                                           ct,
                                           ReportCategory.UI,
                                           signInfo: WebRequestSignInfo.NewFromUrl(fullUrl, unixTimestamp, "patch"),
                                           headersInfo: new WebRequestHeadersInfo().WithSign(string.Empty, unixTimestamp))
                                      .WithCustomExceptionAsync(static exc => new PlacesAPIException(exc, "Error setting place favorite:"));
        }

        public async UniTask RatePlaceAsync(bool? isUpvote, string placeId, CancellationToken ct)
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

            var fullUrl = string.Format(url, placeId);
            ulong unixTimestamp = DateTime.UtcNow.UnixTimeAsMilliseconds();

            await webRequestController.PatchAsync(
                                           fullUrl,
                                           GenericPatchArguments.CreateJson(payload),
                                           ct,
                                           ReportCategory.UI,
                                           signInfo: WebRequestSignInfo.NewFromUrl(fullUrl, unixTimestamp, "patch"),
                                           headersInfo: new WebRequestHeadersInfo().WithSign(string.Empty, unixTimestamp))
                                      .WithCustomExceptionAsync(static exc => new PlacesAPIException(exc, "Error setting place vote:"));
        }

        public async UniTask<List<string>> GetPointsOfInterestCoordsAsync(CancellationToken ct)
        {
            GenericDownloadHandlerUtils.Adapter<GenericPostRequest, GenericPostArguments> result = webRequestController.PostAsync(
                poiURL, GenericPostArguments.Empty, ct, ReportCategory.UI);

            PointsOfInterestCoordsAPIResponse response = await result.CreateFromJson<PointsOfInterestCoordsAPIResponse>(WRJsonParser.Unity,
                createCustomExceptionOnFailure: static (_, text) => new PlacesAPIException("Error parsing get POIs response:", text));

            if (response.data == null)
                throw new Exception("No POIs info retrieved");

            return response.data;
        }

        public async UniTask<IReadOnlyList<OptimizedPlaceInMapResponse>> GetOptimizedPlacesFromTheMap(string category, CancellationToken ct)
        {
            var url = $"{mapApiUrl}?categories={category}";

            List<OptimizedPlaceInMapResponse> categoryPlaces = await webRequestController.GetAsync(url, ct, ReportCategory.UI)
                                                                                 .CreateFromNewtonsoftJsonAsync<List<OptimizedPlaceInMapResponse>>(serializerSettings: SERIALIZER_SETTINGS);

            if (categoryPlaces == null)
                throw new Exception($"No Places for category {category} retrieved");

            return categoryPlaces;
        }

        public async UniTask ReportPlaceAsync(PlaceContentReportPayload placeContentReportPayload, CancellationToken ct)
        {
            // POST for getting a signed url
            GenericDownloadHandlerUtils.Adapter<GenericPostRequest, GenericPostArguments> postResult = webRequestController.PostAsync(
                contentModerationReportURL, GenericPostArguments.Empty, ct, ReportCategory.UI);

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
