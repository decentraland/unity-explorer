using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Multiplayer.Connectivity
{
    public class ArchipelagoHttpOnlineUsersProvider : IOnlineUsersProvider
    {
        private static readonly JsonSerializerSettings SERIALIZER_SETTINGS = new () { Converters = new JsonConverter[] { new OnlinePlayersJsonDtoConverter() } };

        private readonly IWebRequestController webRequestController;
        private readonly URLAddress baseUrl;

        public ArchipelagoHttpOnlineUsersProvider(
            IWebRequestController webRequestController,
            URLAddress baseUrl)
        {
            this.webRequestController = webRequestController;
            this.baseUrl = baseUrl;
        }

        public async UniTask<IReadOnlyCollection<OnlineUserData>> GetAsync(CancellationToken ct) =>
            await webRequestController.GetAsync(baseUrl, ct, ReportCategory.MULTIPLAYER)
                                      .CreateFromNewtonsoftJsonAsync<List<OnlineUserData>>(serializerSettings: SERIALIZER_SETTINGS);
    }
}
