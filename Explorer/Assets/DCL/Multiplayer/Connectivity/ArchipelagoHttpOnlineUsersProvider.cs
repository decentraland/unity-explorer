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
        private readonly URLBuilder urlBuilder = new ();

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

        public async UniTask<IReadOnlyCollection<OnlineUserData>> GetAsync(IEnumerable<string> userIds, CancellationToken ct)
        {
            urlBuilder.Clear();
            urlBuilder.AppendDomain(URLDomain.FromString(baseUrl));

            foreach (string userId in userIds)
                urlBuilder.AppendParameter(new URLParameter("id", userId));

            return await webRequestController.GetAsync(urlBuilder.Build(), ct, ReportCategory.MULTIPLAYER)
                                         .CreateFromNewtonsoftJsonAsync<List<OnlineUserData>>(serializerSettings: SERIALIZER_SETTINGS);
        }
    }
}
