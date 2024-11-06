using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
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

        public async UniTask<IReadOnlyList<EventDTO>> GetEventsByParcelsAsync(ISet<string> parcels, CancellationToken ct)
        {
            urlBuilder.Clear();
            urlBuilder.AppendDomain(baseUrl);

            foreach (string parcel in parcels)
                urlBuilder.AppendParameter(new URLParameter("positions[]", parcel));

            URLAddress url = urlBuilder.Build();

            GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> result = webRequestController.GetAsync(
                url, ct, ReportCategory.EVENTS);

            var response = await result.CreateFromJson<EventDTOListResponse>(WRJsonParser.Unity,
                createCustomExceptionOnFailure: static (e, text) => new EventsApiException($"Error fetching events: {text}", e));

            if (!response.ok)
                throw new EventsApiException("Error fetching events");

            return response.data;
        }
    }
}
