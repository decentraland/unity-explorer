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

        private Uri baseURL => decentralandUrlsSource.Url(DecentralandUrl.ApiPlaces);
        private Uri poiURL => decentralandUrlsSource.Url(DecentralandUrl.POI);
        private Uri mapApiUrl => decentralandUrlsSource.Url(DecentralandUrl.Map);
        private Uri contentModerationReportURL => decentralandUrlsSource.Url(DecentralandUrl.ContentModerationReport);

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
                urlBuilder.AppendParameter(new URLParameter("categories", category.ToLower()));

            if (onlyFavorites != null)
                urlBuilder.AppendParameter(ONLY_FAVORITES);

            if (addRealmDetails != null)
                urlBuilder.AppendParameter(WITH_REALMS_DETAIL);

            if (positions != null)
                foreach (string xy in positions)
                    urlBuilder.AppendParameter(new URLParameter("positions", xy));

            Uri url = urlBuilder.Build();

            ulong timestamp = DateTime.UtcNow.UnixTimeAsMilliseconds();

            var result = webRequestController.GetAsync(
                url,
                ReportCategory.UI,
                signInfo: WebRequestSignInfo.NewFromUrl(url, timestamp, "get"),
                headersInfo: new WebRequestHeadersInfo().WithSign(string.Empty, timestamp));

            PlacesData.PlacesAPIResponse response = PlacesData.PLACES_API_RESPONSE_POOL.Get();

            await result.OverwriteFromJsonAsync(response, WRJsonParser.Unity, ct,
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
            Uri url = baseURL.Append($"/{placeId}?with_realms_detail=true");

            var result = webRequestController.GetAsync(
                url, ReportCategory.UI,
                signInfo: WebRequestSignInfo.NewFromUrl(url, timestamp, "get"),
                headersInfo: new WebRequestHeadersInfo().WithSign(string.Empty, timestamp));

            PlacesData.PlacesAPIGetParcelResponse response = await result.CreateFromJsonAsync<PlacesData.PlacesAPIGetParcelResponse>(WRJsonParser.Unity, ct,
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
            Uri url = baseURL.Append($"/{placeId}/favorites");
            const string FAVORITE_PAYLOAD = "{\"favorites\": true}";
            const string NOT_FAVORITE_PAYLOAD = "{\"favorites\": false}";

            await webRequestController.PatchAsync(
                                           url,
                                           GenericUploadArguments.CreateJson(isFavorite ? FAVORITE_PAYLOAD : NOT_FAVORITE_PAYLOAD),
                                           ReportCategory.UI,
                                           signInfo: WebRequestSignInfo.NewFromUrl(url, unixTimestamp, "patch"),
                                           headersInfo: new WebRequestHeadersInfo().WithSign(string.Empty, unixTimestamp))
                                      .WithCustomExceptionAsync(static exc => new PlacesAPIException(exc, "Error setting place favorite:"), ct);
        }

        public async UniTask RatePlaceAsync(bool? isUpvote, string placeId, CancellationToken ct)
        {
            Uri url = baseURL.Append($"/{placeId}/likes");
            const string LIKE_PAYLOAD = "{\"like\": true}";
            const string DISLIKE_PAYLOAD = "{\"like\": false}";
            const string NO_LIKE_PAYLOAD = "{\"like\": null}";

            string payload;

            if (isUpvote == null)
                payload = NO_LIKE_PAYLOAD;
            else
                payload = isUpvote == true ? LIKE_PAYLOAD : DISLIKE_PAYLOAD;

            ulong unixTimestamp = DateTime.UtcNow.UnixTimeAsMilliseconds();

            await webRequestController.PatchAsync(
                                           url,
                                           GenericUploadArguments.CreateJson(payload),
                                           ReportCategory.UI,
                                           signInfo: WebRequestSignInfo.NewFromUrl(url, unixTimestamp, "patch"),
                                           headersInfo: new WebRequestHeadersInfo().WithSign(string.Empty, unixTimestamp))
                                      .WithCustomExceptionAsync(static exc => new PlacesAPIException(exc, "Error setting place vote:"), ct);
        }

        public async UniTask<List<string>> GetPointsOfInterestCoordsAsync(CancellationToken ct)
        {
            GenericPostRequest result = webRequestController.PostAsync(poiURL, GenericUploadArguments.Empty, ReportCategory.UI);

            PointsOfInterestCoordsAPIResponse response = await result.CreateFromJsonAsync<PointsOfInterestCoordsAPIResponse>(WRJsonParser.Unity,
                ct, createCustomExceptionOnFailure: static (_, text) => new PlacesAPIException("Error parsing get POIs response:", text));

            if (response.data == null)
                throw new Exception("No POIs info retrieved");

            return response.data;
        }

        public async UniTask<IReadOnlyList<OptimizedPlaceInMapResponse>> GetOptimizedPlacesFromTheMapAsync(string category, CancellationToken ct)
        {
            var url = new Uri($"{mapApiUrl}?categories={category}");

            List<OptimizedPlaceInMapResponse> categoryPlaces = await webRequestController.GetAsync(url, ReportCategory.UI)
                                                                                         .CreateFromNewtonsoftJsonAsync<List<OptimizedPlaceInMapResponse>>(ct, serializerSettings: SERIALIZER_SETTINGS);

            if (categoryPlaces == null)
                throw new Exception($"No Places for category {category} retrieved");

            return categoryPlaces;
        }

        public async UniTask ReportPlaceAsync(PlaceContentReportPayload placeContentReportPayload, CancellationToken ct)
        {
            // POST for getting a signed url
            GenericPostRequest postResult = webRequestController.PostAsync(contentModerationReportURL, GenericUploadArguments.Empty, ReportCategory.UI);

            using PoolExtensions.Scope<ReportPlaceAPIResponse> responseRental = PlacesData.REPORT_PLACE_API_RESPONSE_POOL.AutoScope();
            ReportPlaceAPIResponse response = responseRental.Value;

            await postResult.OverwriteFromJsonAsync(response, WRJsonParser.Unity, ct,
                                 WRThreadFlags.SwitchToThreadPool, // don't return to the main thread
                                 createCustomExceptionOnFailure: static (_, text) => new PlacesAPIException("Error parsing report place response:", text))
                            .WithCustomExceptionAsync(static exc => new PlacesAPIException(exc, "Error reporting place:"));

            if (response.data == null || !response.ok)
                throw new PlacesAPIException("Error reporting place");

            // PUT using the gotten signed url and sending the report payload

            string putData = JsonUtility.ToJson(placeContentReportPayload);

            await UniTask.SwitchToMainThread();

            await webRequestController.PutAsync(new Uri(response.data.signed_url), GenericUploadArguments.CreateJson(putData), ReportCategory.UI)
                                      .WithCustomExceptionAsync(static exc => new PlacesAPIException(exc, "Error reporting place:"), ct);
        }
    }
}
