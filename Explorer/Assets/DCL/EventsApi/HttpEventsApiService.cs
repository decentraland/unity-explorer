using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
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
    }
}
