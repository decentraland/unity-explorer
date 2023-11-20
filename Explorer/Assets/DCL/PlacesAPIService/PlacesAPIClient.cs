using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Diagnostics;
using UnityEngine.Networking;

namespace DCLServices.PlacesAPIService
{
    public interface IPlacesAPIClient
    {
        UniTask<PlacesData.PlacesAPIResponse> SearchPlaces(string searchString, int pageNumber, int pageSize, CancellationToken ct);
        UniTask<PlacesData.PlacesAPIResponse> GetMostActivePlaces(int pageNumber, int pageSize, string filter = "", string sort = "", CancellationToken ct = default);
        UniTask<PlacesData.PlaceInfo> GetPlace(Vector2Int coords, CancellationToken ct);
        UniTask<PlacesData.PlaceInfo> GetPlace(string placeUUID, CancellationToken ct);
        UniTask<List<PlacesData.PlaceInfo>> GetFavorites(int pageNumber, int pageSize, CancellationToken ct);
        UniTask<List<PlacesData.PlaceInfo>> GetAllFavorites(CancellationToken ct);
        UniTask<List<PlacesData.PlaceInfo>> GetPlacesByCoordsList(List<Vector2Int> coordsList, CancellationToken ct);
        UniTask ReportPlace(PlaceContentReportPayload placeContentReportPayload, CancellationToken ct);


        UniTask SetPlaceFavorite(string placeUUID, bool isFavorite, CancellationToken ct);
        UniTask SetPlaceVote(bool? isUpvote, string placeUUID, CancellationToken ct);
        UniTask<List<string>> GetPointsOfInterestCoords(CancellationToken ct);
    }

    public class PlacesAPIClient: IPlacesAPIClient
    {
        private const string BASE_URL = "https://places.decentraland.org/api/places";
        private const string BASE_URL_ZONE = "https://places.decentraland.zone/api/places";
        private const string POI_URL = "https://dcl-lists.decentraland.org/pois";
        private const string CONTENT_MODERATION_REPORT_URL = "https://places.decentraland.org/api/report";
        private readonly IWebRequestController webRequestController;

        public PlacesAPIClient(IWebRequestController webRequestController)
        {
            this.webRequestController = webRequestController;
        }

        public async UniTask<PlacesData.PlacesAPIResponse> SearchPlaces(string searchString, int pageNumber, int pageSize, CancellationToken ct)
        {
            const string URL = BASE_URL + "?with_realms_detail=true&search={0}&offset={1}&limit={2}";
            var result = await webRequestController.GetAsync(
                new CommonArguments(URLDomain.FromString(string.Format(URL, searchString.Replace(" ", "+"), pageNumber * pageSize, pageSize))),
                ct);

            if (result.UnityWebRequest.result != UnityWebRequest.Result.Success)
                throw new Exception($"Error fetching most active places info:\n{result.UnityWebRequest.error}");

            var response = JsonUtils.SafeFromJson<PlacesData.PlacesAPIResponse>(result.UnityWebRequest.downloadHandler.text);

            if (response == null)
                throw new Exception($"Error parsing place info:\n{result.UnityWebRequest.downloadHandler.text}");

            if (response.data == null)
                throw new Exception($"No place info retrieved:\n{result.UnityWebRequest.downloadHandler.text}");

            return response;
        }

        public async UniTask<PlacesData.PlacesAPIResponse> GetMostActivePlaces(int pageNumber, int pageSize, string filter = "", string sort = "", CancellationToken ct = default)
        {
            const string URL = BASE_URL + "?order_by={3}&order=desc&with_realms_detail=true&offset={0}&limit={1}&{2}";
            var result = await webRequestController.GetAsync(
                new CommonArguments(URLDomain.FromString(string.Format(URL, pageNumber * pageSize, pageSize, filter, sort))),
                ct);

            if (result.UnityWebRequest.result != UnityWebRequest.Result.Success)
                throw new Exception($"Error fetching most active places info:\n{result.UnityWebRequest.error}");

            var response = JsonUtils.SafeFromJson<PlacesData.PlacesAPIResponse>(result.UnityWebRequest.downloadHandler.text);

            if (response == null)
                throw new Exception($"Error parsing place info:\n{result.UnityWebRequest.downloadHandler.text}");

            if (response.data == null)
                throw new Exception($"No place info retrieved:\n{result.UnityWebRequest.downloadHandler.text}");

            return response;
        }

        public async UniTask<PlacesData.PlaceInfo> GetPlace(Vector2Int coords, CancellationToken ct)
        {
            const string URL = BASE_URL + "?position={0},{1}&with_realms_detail=true";
            
            var result = await webRequestController.GetAsync(
                new CommonArguments(URLDomain.FromString(string.Format(URL, coords.x, coords.y))),
                ct);

            if (result.UnityWebRequest.result != UnityWebRequest.Result.Success)
                throw new Exception($"Error fetching place info:\n{result.UnityWebRequest.error}");

            var response = JsonUtils.SafeFromJson<PlacesData.PlacesAPIResponse>(result.UnityWebRequest.downloadHandler.text);

            if (response == null)
                throw new Exception($"Error parsing place info:\n{result.UnityWebRequest.downloadHandler.text}");

            if (response.data.Count == 0)
                throw new NotAPlaceException(coords);

            return response.data[0];
        }

        public async UniTask<PlacesData.PlaceInfo> GetPlace(string placeUUID, CancellationToken ct)
        {
            var url = $"{BASE_URL}/{placeUUID}?with_realms_detail=true";
            var result = await webRequestController.GetAsync(
                new CommonArguments(URLDomain.FromString(url)),
                ct);

            if (result.UnityWebRequest.result != UnityWebRequest.Result.Success)
                throw new Exception($"Error fetching place info:\n{result.UnityWebRequest.error}");

            var response = JsonUtils.SafeFromJson<PlacesData.PlacesAPIGetParcelResponse>(result.UnityWebRequest.downloadHandler.text);

            if (response == null)
                throw new Exception($"Error parsing place info:\n{result.UnityWebRequest.downloadHandler.text}");

            if (response.ok == false)
                throw new NotAPlaceException(placeUUID);

            if (response.data == null)
                throw new Exception($"No place info retrieved:\n{result.UnityWebRequest.downloadHandler.text}");

            return response.data;
        }

        public async UniTask<List<PlacesData.PlaceInfo>> GetPlacesByCoordsList(List<Vector2Int> coordsList, CancellationToken ct)
        {
            if (coordsList.Count == 0)
                return new List<PlacesData.PlaceInfo>();

            var url = string.Concat(BASE_URL, "?");
            foreach (Vector2Int coords in coordsList)
                url = string.Concat(url, $"positions={coords.x},{coords.y}&with_realms_detail=true&");

            var result = await webRequestController.GetAsync(
                new CommonArguments(URLDomain.FromString(url)),
                ct);

            if (result.UnityWebRequest.result != UnityWebRequest.Result.Success)
                throw new Exception($"Error fetching places info:\n{result.UnityWebRequest.error}");

            var response = JsonUtils.SafeFromJson<PlacesData.PlacesAPIResponse>(result.UnityWebRequest.downloadHandler.text);

            if (response == null)
                throw new Exception($"Error parsing places info:\n{result.UnityWebRequest.downloadHandler.text}");

            return response.data;
        }

        public async UniTask<List<PlacesData.PlaceInfo>> GetFavorites(int pageNumber, int pageSize, CancellationToken ct)
        {
            const string URL = BASE_URL + "?only_favorites=true&with_realms_detail=true&offset={0}&limit={1}";

            GenericGetRequest result = await webRequestController.GetAsync(
                new CommonArguments(URLDomain.FromString(string.Format(URL, pageNumber * pageSize, pageSize))),
                ct);

            var response = JsonUtils.SafeFromJson<PlacesData.PlacesAPIResponse>(result.UnityWebRequest.downloadHandler.text);

            if (response == null)
                throw new Exception($"Error parsing get favorites response:\n{result.UnityWebRequest.downloadHandler.text}");

            if (response.data == null)
                throw new Exception($"No favorites info retrieved:\n{result.UnityWebRequest.downloadHandler.text}");

            return response.data;
        }

        public async UniTask<List<PlacesData.PlaceInfo>> GetAllFavorites(CancellationToken ct)
        {
            const string URL = BASE_URL + "?only_favorites=true&with_realms_detail=true";

            GenericGetRequest result = await webRequestController.GetAsync(
                new CommonArguments(URLDomain.FromString(URL)),
                ct);

            var response = JsonUtils.SafeFromJson<PlacesData.PlacesAPIResponse>(result.UnityWebRequest.downloadHandler.text);

            if (response == null)
                throw new Exception($"Error parsing get favorites response:\n{result.UnityWebRequest.downloadHandler.text}");

            if (response.data == null)
                throw new Exception($"No favorites info retrieved:\n{result.UnityWebRequest.downloadHandler.text}");

            return response.data;
        }

        public async UniTask SetPlaceFavorite(string placeUUID, bool isFavorite, CancellationToken ct)
        {
            const string URL = BASE_URL + "/{0}/favorites";
            const string FAVORITE_PAYLOAD = "{\"favorites\": true}";
            const string NOT_FAVORITE_PAYLOAD = "{\"favorites\": false}";

            GenericPatchRequest result = await webRequestController.PatchAsync(
                new CommonArguments(URLDomain.FromString(string.Format(URL, placeUUID))),
                GenericPatchArguments.CreateJson(isFavorite ? FAVORITE_PAYLOAD : NOT_FAVORITE_PAYLOAD),
                ct);

            if (result.UnityWebRequest.result != UnityWebRequest.Result.Success)
                throw new Exception($"Error fetching place info:\n{result.UnityWebRequest.error}");
        }

        public async UniTask SetPlaceVote(bool? isUpvote, string placeUUID, CancellationToken ct)
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

            GenericPostRequest result = await webRequestController.PostAsync(
                new CommonArguments(URLDomain.FromString(string.Format(URL, placeUUID))),
                GenericPostArguments.CreateJson(payload),
                ct);

            if (result.UnityWebRequest.result != UnityWebRequest.Result.Success)
                throw new Exception($"Error fetching place info:\n{result.UnityWebRequest.error}");
        }

        public async UniTask<List<string>> GetPointsOfInterestCoords(CancellationToken ct)
        {
            GenericPostRequest result = await webRequestController.PostAsync(
                new CommonArguments(URLDomain.FromString(POI_URL)),
                GenericPostArguments.CreateJson(""),
                ct);

            var response = JsonUtils.SafeFromJson<PointsOfInterestCoordsAPIResponse>(result.UnityWebRequest.downloadHandler.text);

            if (response == null)
                throw new Exception($"Error parsing get POIs response:\n{result.UnityWebRequest.downloadHandler.text}");

            if (response.data == null)
                throw new Exception($"No POIs info retrieved:\n{result.UnityWebRequest.downloadHandler.text}");

            return response.data;
        }

        public async UniTask ReportPlace(PlaceContentReportPayload placeContentReportPayload, CancellationToken ct)
        {
            // POST for getting a signed url
            GenericPostRequest postResult = await webRequestController.PostAsync(
                new CommonArguments(URLDomain.FromString(CONTENT_MODERATION_REPORT_URL)),
                GenericPostArguments.CreateJson(""),
                ct);

            if (postResult.UnityWebRequest.result != UnityWebRequest.Result.Success)
                throw new Exception($"Error reporting place:\n{postResult.UnityWebRequest.error}");

            var postResponse = JsonUtils.SafeFromJson<ReportPlaceAPIResponse>(postResult.UnityWebRequest.downloadHandler.text);
            if (postResponse?.data == null || !postResponse.ok)
                throw new Exception($"Error reporting place:\n{postResult.UnityWebRequest.downloadHandler.text}");

            // PUT using the gotten signed url and sending the report payload
            string putData = JsonUtility.ToJson(placeContentReportPayload);
            GenericPutRequest putResult = await webRequestController.PutAsync(
                new CommonArguments(URLDomain.FromString(postResponse.data.signed_url)),
                GenericPutArguments.CreateJson(putData),
                ct);

            if (putResult.UnityWebRequest.result != UnityWebRequest.Result.Success)
                throw new Exception($"Error reporting place:\n{putResult.UnityWebRequest.error}");
        }
    }
}
