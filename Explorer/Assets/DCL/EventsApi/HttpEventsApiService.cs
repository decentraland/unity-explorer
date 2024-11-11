using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using DCL.WebRequests.GenericDelete;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.EventsApi
{
    public class HttpEventsApiService : IEventsApiService
    {
        private readonly IWebRequestController webRequestController;
        private readonly URLDomain baseUrl;
        private readonly URLBuilder urlBuilder = new ();

        public HttpEventsApiService(IWebRequestController webRequestController,
            URLDomain baseUrl)
        {
            this.webRequestController = webRequestController;
            this.baseUrl = baseUrl;
        }

        public async UniTask<IReadOnlyList<EventDTO>> GetEventsByParcelAsync(ISet<string> parcels, CancellationToken ct,
            bool onlyLiveEvents = false)
        {
            urlBuilder.Clear();
            urlBuilder.AppendDomain(baseUrl);

            foreach (string parcel in parcels)
                urlBuilder.AppendParameter(new URLParameter("positions[]", parcel));

            if (onlyLiveEvents)
                urlBuilder.AppendParameter(new URLParameter("list", "live"));

            return await FetchEventList(urlBuilder.Build(), ct);
        }

        public async UniTask<IReadOnlyList<EventDTO>> GetEventsByParcelAsync(string parcel, CancellationToken ct,
            bool onlyLiveEvents = false)
        {
            urlBuilder.Clear();
            urlBuilder.AppendDomain(baseUrl);
            urlBuilder.AppendParameter(new URLParameter("position", parcel));

            if (onlyLiveEvents)
                urlBuilder.AppendParameter(new URLParameter("list", "live"));

            return await FetchEventList(urlBuilder.Build(), ct);
        }

        public async UniTask MarkAsInterestedAsync(string eventId, CancellationToken ct)
        {
            urlBuilder.Clear();
            urlBuilder.AppendDomain(baseUrl);
            urlBuilder.AppendPath(URLPath.FromString($"{eventId}/attendees"));
            GenericDownloadHandlerUtils.Adapter<GenericPostRequest, GenericPostArguments> result = webRequestController.PostAsync(
                urlBuilder.Build(), GenericPostArguments.Empty, ct, ReportCategory.EVENTS);

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

            GenericDownloadHandlerUtils.Adapter<GenericDeleteRequest, GenericDeleteArguments> result = webRequestController.DeleteAsync(
                urlBuilder.Build(), GenericDeleteArguments.Empty, ct, ReportCategory.EVENTS);

            var response = await result.CreateFromJson<AttendResponse>(WRJsonParser.Unity,
                createCustomExceptionOnFailure: static (e, text) => new EventsApiException($"Error on trying to create attend intention: {text}", e));

            if (!response.ok)
                throw new EventsApiException($"Error on trying to create attend intention to event {eventId}");
        }

        private async UniTask<IReadOnlyList<EventDTO>> FetchEventList(URLAddress url, CancellationToken ct)
        {
            GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> result = webRequestController.GetAsync(
                url, ct, ReportCategory.EVENTS);

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
