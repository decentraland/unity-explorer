using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;
using Utility.Times;

namespace DCL.EventsApi
{
    public class HttpEventsApiService : IEventsApiService
    {
        private const string LIVE_PARAMETER_VALUE = "live";
        private const string LIST_PARAMETER = "list";
        private const string POSITION_PARAMETER = "position";
        private const string POSITIONS_PARAMETER = "positions[]";
        private const string PLACE_ID_PARAMETER = "places_ids[]";
        private readonly IWebRequestController webRequestController;
        private readonly URLDomain baseUrl;
        private readonly URLBuilder urlBuilder = new ();
        private readonly StringBuilder placeIdsBuilder = new ();

        public HttpEventsApiService(IWebRequestController webRequestController,
            URLDomain baseUrl)
        {
            this.webRequestController = webRequestController;
            this.baseUrl = baseUrl;
        }

        public async UniTask<IReadOnlyList<EventDTO>> GetEventsAsync(CancellationToken ct, bool onlyLiveEvents = false)
        {
            urlBuilder.Clear();
            urlBuilder.AppendDomain(baseUrl);

            if (onlyLiveEvents)
                urlBuilder.AppendParameter(new URLParameter(LIST_PARAMETER, LIVE_PARAMETER_VALUE));

            return await FetchEventListAsync(urlBuilder.Build(), ct);
        }

        public async UniTask<IReadOnlyList<EventDTO>> GetEventsByParcelAsync(IEnumerable<Vector2Int> parcels, CancellationToken ct, bool onlyLiveEvents = false)
        {
            urlBuilder.Clear();
            urlBuilder.AppendDomain(baseUrl);

            foreach (Vector2Int parcel in parcels)
                urlBuilder.AppendParameter(new URLParameter(POSITIONS_PARAMETER, $"{parcel.x},{parcel.y}"));

            if (onlyLiveEvents)
                urlBuilder.AppendParameter(new URLParameter(LIST_PARAMETER, LIVE_PARAMETER_VALUE));

            return await FetchEventListAsync(urlBuilder.Build(), ct);
        }

        public async UniTask<IReadOnlyList<EventDTO>> GetEventsByParcelAsync(ISet<string> parcels, CancellationToken ct,
            bool onlyLiveEvents = false)
        {
            urlBuilder.Clear();
            urlBuilder.AppendDomain(baseUrl);

            foreach (string parcel in parcels)
                urlBuilder.AppendParameter(new URLParameter(POSITIONS_PARAMETER, parcel));

            if (onlyLiveEvents)
                urlBuilder.AppendParameter(new URLParameter(LIST_PARAMETER, LIVE_PARAMETER_VALUE));

            return await FetchEventListAsync(urlBuilder.Build(), ct);
        }

        public async UniTask<EventWithPlaceIdDTOListResponse> GetEventsByPlaceIdsAsync(string[] placeIds, int pageNumber, int elementsPerPage, CancellationToken ct)
        {
            urlBuilder.Clear();
            urlBuilder.AppendDomain(baseUrl)
                      .AppendSubDirectory(URLSubdirectory.FromString("by-places"))
                      .AppendParameter(new URLParameter("limit", elementsPerPage.ToString()))
                      .AppendParameter(new URLParameter("offset", ((pageNumber - 1) * elementsPerPage).ToString()));

            placeIdsBuilder.Clear();

            placeIdsBuilder.Append("[");
            for (int i = 0; i < placeIds.Length; i++)
            {
                placeIdsBuilder.Append($"\"{placeIds[i]}\"");
                if (i < placeIds.Length - 1)
                    placeIdsBuilder.Append(",");
            }
            placeIdsBuilder.Append("]");

            URLAddress url = urlBuilder.Build();

            EventWithPlaceIdDTOListResponse responseData = await webRequestController
                                                                .SignedFetchPostAsync(url,  GenericPostArguments.CreateJson(placeIdsBuilder.ToString()), string.Empty, ct)
                                                                .CreateFromJson<EventWithPlaceIdDTOListResponse>(WRJsonParser.Unity);

            return responseData;
        }


        public async UniTask MarkAsInterestedAsync(string eventId, CancellationToken ct)
        {
            urlBuilder.Clear();
            urlBuilder.AppendDomain(baseUrl);
            urlBuilder.AppendPath(URLPath.FromString($"{eventId}/attendees"));
            Uri url = urlBuilder.Build();
            ulong timestamp = DateTime.UtcNow.UnixTimeAsMilliseconds();

            var result = webRequestController.PostAsync(
                url, GenericUploadArguments.Empty, ReportCategory.EVENTS,
                signInfo: WebRequestSignInfo.NewFromUrl(url, timestamp, "post"),
                headersInfo: new WebRequestHeadersInfo().WithSign(string.Empty, timestamp));

            var response = await result.CreateFromJsonAsync<AttendResponse>(WRJsonParser.Unity,
                ct,
                createCustomExceptionOnFailure: static (e, text) => new EventsApiException($"Error on trying to create attend intention: {text}", e));

            if (!response.ok)
                throw new EventsApiException($"Error on trying to create attend intention to event {eventId}");
        }

        public async UniTask MarkAsNotInterestedAsync(string eventId, CancellationToken ct)
        {
            urlBuilder.Clear();
            urlBuilder.AppendDomain(baseUrl);
            urlBuilder.AppendPath(URLPath.FromString($"{eventId}/attendees"));
            Uri url = urlBuilder.Build();
            ulong timestamp = DateTime.UtcNow.UnixTimeAsMilliseconds();

            var result = webRequestController.DeleteAsync(
                url, GenericUploadArguments.Empty, ReportCategory.EVENTS,
                signInfo: WebRequestSignInfo.NewFromUrl(url, timestamp, "delete"),
                headersInfo: new WebRequestHeadersInfo().WithSign(string.Empty, timestamp));

            var response = await result.CreateFromJsonAsync<AttendResponse>(WRJsonParser.Unity,
                ct,
                createCustomExceptionOnFailure: static (e, text) => new EventsApiException($"Error on trying to create attend intention: {text}", e));

            if (!response.ok)
                throw new EventsApiException($"Error on trying to create attend intention to event {eventId}");
        }

        private async UniTask<IReadOnlyList<EventDTO>> FetchEventListAsync(Uri url, CancellationToken ct)
        {
            ulong timestamp = DateTime.UtcNow.UnixTimeAsMilliseconds();

            var result = webRequestController.GetAsync(
                url, ReportCategory.EVENTS,
                signInfo: WebRequestSignInfo.NewFromUrl(url, timestamp, "post"),
                headersInfo: new WebRequestHeadersInfo().WithSign(string.Empty, timestamp));

            var response = await result.CreateFromJsonAsync<EventDTOListResponse>(WRJsonParser.Unity,
                ct,
                createCustomExceptionOnFailure: static (e, text) => new EventsApiException($"Error fetching events: {text}", e));

            if (!response.ok)
                throw new EventsApiException("Error fetching events");

            return response.data ?? Array.Empty<EventDTO>();
        }

        [Serializable]
        private struct AttendResponse
        {
            public bool ok;
        }
    }
}
