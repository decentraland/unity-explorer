using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using DCL.WebRequests.GenericDelete;
using System;
using System.Collections.Generic;
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
        private readonly IWebRequestController webRequestController;
        private readonly URLDomain baseUrl;
        private readonly URLBuilder urlBuilder = new ();

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

        public async UniTask MarkAsInterestedAsync(string eventId, CancellationToken ct)
        {
            urlBuilder.Clear();
            urlBuilder.AppendDomain(baseUrl);
            urlBuilder.AppendPath(URLPath.FromString($"{eventId}/attendees"));
            URLAddress url = urlBuilder.Build();
            ulong timestamp = DateTime.UtcNow.UnixTimeAsMilliseconds();

            GenericDownloadHandlerUtils.Adapter<GenericPostRequest, GenericPostArguments> result = webRequestController.PostAsync(
                url, GenericPostArguments.Empty, ct, ReportCategory.EVENTS,
                signInfo: WebRequestSignInfo.NewFromUrl(url, timestamp, "post"),
                headersInfo: new WebRequestHeadersInfo().WithSign(string.Empty, timestamp));

            var response = await result.CreateFromJson<AttendResponse>(WRJsonParser.Unity,
                createCustomExceptionOnFailure: static (e, text) => new EventsApiException($"Error on trying to create attend intention: {text}", e));

            if (!response.ok)
                throw new EventsApiException($"Error on trying to create attend intention to event {eventId}");
        }

        public async UniTask MarkAsNotInterestedAsync(string eventId, CancellationToken ct)
        {
            urlBuilder.Clear();
            urlBuilder.AppendDomain(baseUrl);
            urlBuilder.AppendPath(URLPath.FromString($"{eventId}/attendees"));
            URLAddress url = urlBuilder.Build();
            ulong timestamp = DateTime.UtcNow.UnixTimeAsMilliseconds();

            GenericDownloadHandlerUtils.Adapter<GenericDeleteRequest, GenericDeleteArguments> result = webRequestController.DeleteAsync(
                url, GenericDeleteArguments.Empty, ct, ReportCategory.EVENTS,
                signInfo: WebRequestSignInfo.NewFromUrl(url, timestamp, "delete"),
                headersInfo: new WebRequestHeadersInfo().WithSign(string.Empty, timestamp));

            var response = await result.CreateFromJson<AttendResponse>(WRJsonParser.Unity,
                createCustomExceptionOnFailure: static (e, text) => new EventsApiException($"Error on trying to create attend intention: {text}", e));

            if (!response.ok)
                throw new EventsApiException($"Error on trying to create attend intention to event {eventId}");
        }

        private async UniTask<IReadOnlyList<EventDTO>> FetchEventListAsync(URLAddress url, CancellationToken ct)
        {
            ulong timestamp = DateTime.UtcNow.UnixTimeAsMilliseconds();

            GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> result = webRequestController.GetAsync(
                url, ct, ReportCategory.EVENTS,
                signInfo: WebRequestSignInfo.NewFromUrl(url, timestamp, "post"),
                headersInfo: new WebRequestHeadersInfo().WithSign(string.Empty, timestamp));

            var response = await result.CreateFromJson<EventDTOListResponse>(WRJsonParser.Unity,
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
