using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Multiplayer.Connectivity
{
    public class ArchipelagoHttpOnlineUsersProvider : IOnlineUsersProvider
    {
        private static readonly JsonSerializerSettings SERIALIZER_SETTINGS = new () { Converters = new JsonConverter[] { new OnlinePlayersJsonDtoConverter() } };

        private readonly IWebRequestController webRequestController;
        private readonly Uri baseUrl;
        private readonly URLBuilder urlBuilder = new ();

        public ArchipelagoHttpOnlineUsersProvider(
            IWebRequestController webRequestController,
            Uri baseUrl)
        {
            this.webRequestController = webRequestController;
            this.baseUrl = baseUrl;
        }

        public async UniTask<IReadOnlyCollection<OnlineUserData>> GetAsync(CancellationToken ct) =>
            await webRequestController.GetAsync(baseUrl, ReportCategory.MULTIPLAYER)
                                      .CreateFromNewtonsoftJsonAsync<List<OnlineUserData>>(ct, serializerSettings: SERIALIZER_SETTINGS);

        public async UniTask<IReadOnlyCollection<OnlineUserData>> GetAsync(IEnumerable<string> userIds, CancellationToken ct)
        {
            urlBuilder.Clear();
            urlBuilder.AppendDomain(URLDomain.FromString(baseUrl.OriginalString));

            foreach (string userId in userIds)
                urlBuilder.AppendParameter(new URLParameter("id", userId));

            return await webRequestController.GetAsync(urlBuilder.Build(), ReportCategory.MULTIPLAYER)
                                         .CreateFromNewtonsoftJsonAsync<List<OnlineUserData>>(ct, serializerSettings: SERIALIZER_SETTINGS);
        }
    }
}
