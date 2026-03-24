using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Threading;

namespace DCL.VoiceChat.MutePersistence
{
    public class RestProximityMuteRepository : IProximityMuteRepository
    {
        private const int PAGE_SIZE = 100;
        private const string TAG = nameof(RestProximityMuteRepository);

        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource urlsSource;

        private string mutesUrl => urlsSource.Url(DecentralandUrl.SocialServiceMutes);

        public RestProximityMuteRepository(
            IWebRequestController webRequestController,
            IDecentralandUrlsSource urlsSource)
        {
            this.webRequestController = webRequestController;
            this.urlsSource = urlsSource;
        }

        public async UniTask<List<string>> GetAllMutedUsersAsync(CancellationToken ct)
        {
            var allMuted = new List<string>();
            var offset = 0;

            while (true)
            {
                if (ct.IsCancellationRequested)
                    return allMuted;

                string url = $"{mutesUrl}?limit={PAGE_SIZE}&offset={offset}";

                GetMutesResponse response = await webRequestController
                    .SignedFetchGetAsync(url, string.Empty, ct)
                    .CreateFromJson<GetMutesResponse>(WRJsonParser.Newtonsoft);

                if (response.Data.Results != null)
                {
                    foreach (GetMutesResponse.MutedUserEntry entry in response.Data.Results)
                    {
                        if (!string.IsNullOrEmpty(entry.Address))
                            allMuted.Add(entry.Address);
                    }
                }

                offset += PAGE_SIZE;

                if (offset >= response.Data.Total)
                    break;
            }

            ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Loaded {allMuted.Count} muted users from API");
            return allMuted;
        }

        public async UniTask MuteUserAsync(string walletAddress, CancellationToken ct)
        {
            string body = JsonConvert.SerializeObject(new MuteRequestBody(walletAddress));

            await webRequestController
                .SignedFetchPostAsync(mutesUrl, GenericPostArguments.CreateJson(body), string.Empty, ct)
                .WithNoOpAsync();

            ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Muted {walletAddress} via API");
        }

        public async UniTask UnmuteUserAsync(string walletAddress, CancellationToken ct)
        {
            string body = JsonConvert.SerializeObject(new MuteRequestBody(walletAddress));

            await webRequestController
                .SignedFetchDeleteAsync(mutesUrl, GenericPostArguments.CreateJson(body), string.Empty, ct)
                .WithNoOpAsync();

            ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Unmuted {walletAddress} via API");
        }
    }
}
