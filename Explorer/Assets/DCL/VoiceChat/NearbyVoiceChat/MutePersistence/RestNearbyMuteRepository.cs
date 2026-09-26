using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.VoiceChat.Nearby.MutePersistence
{
    public class RestNearbyMuteRepository : INearbyMuteRepository
    {
        internal const int PAGE_SIZE = 100;
        internal const int MAX_PAGES = 100; // safety cap: up to 10k muted users
        private const string TAG = nameof(RestNearbyMuteRepository);

        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource urlsSource;

        private string mutesUrl => urlsSource.Url(DecentralandUrl.SocialServiceMutes);

        public RestNearbyMuteRepository(IWebRequestController webRequestController, IDecentralandUrlsSource urlsSource)
        {
            this.webRequestController = webRequestController;
            this.urlsSource = urlsSource;
        }

        public async UniTask<List<string>> GetAllMutedUsersAsync(CancellationToken ct) =>
            await PaginateMutesAsync(FetchPageAsync, ct);

        // Extracted for testability of PaginateMutesAsync function
        private UniTask<GetMutesResponse> FetchPageAsync(int offset, CancellationToken ct) =>
            webRequestController
                .SignedFetchGetAsync($"{mutesUrl}?limit={PAGE_SIZE}&offset={offset}", string.Empty, ct)
                .CreateFromJson<GetMutesResponse>(WRJsonParser.Newtonsoft);

        /// <summary>
        /// Pure pagination loop. Safeguards:
        ///  - hard cap at <see cref="MAX_PAGES"/> iterations (immune to runaway Total from the API),
        ///  - breaks on null/empty page (defense against a lying Total),
        ///  - advances offset by the actual Results.Length (not a fixed PAGE_SIZE), so short pages don't lose entries.
        /// Exposed as internal static so tests can drive it with a fake fetcher without mocking the web stack.
        /// </summary>
        internal static async UniTask<List<string>> PaginateMutesAsync(Func<int, CancellationToken, UniTask<GetMutesResponse>> fetchPage, CancellationToken ct)
        {
            var allMuted = new List<string>();
            var offset = 0;

            for (var page = 0; page < MAX_PAGES; page++)
            {
                if (ct.IsCancellationRequested)
                    return allMuted;

                GetMutesResponse response = await fetchPage(offset, ct);

                GetMutesResponse.MutedUserEntry[]? results = response.Data.Results;

                if (results == null || results.Length == 0)
                    break;

                foreach (GetMutesResponse.MutedUserEntry entry in results)
                {
                    if (!string.IsNullOrEmpty(entry.Address))
                        allMuted.Add(entry.Address);
                }

                offset += results.Length;

                if (offset >= response.Data.Total)
                    break;
            }

            return allMuted;
        }

        public UniTask MuteUserAsync(string walletAddress, CancellationToken ct) =>
            SendMuteRequestAsync(walletAddress, mute: true, ct);

        public UniTask UnmuteUserAsync(string walletAddress, CancellationToken ct) =>
            SendMuteRequestAsync(walletAddress, mute: false, ct);

        private async UniTask SendMuteRequestAsync(string walletAddress, bool mute, CancellationToken ct)
        {
            string body = JsonConvert.SerializeObject(new MuteRequestBody(walletAddress));
            GenericPostArguments args = GenericPostArguments.CreateJson(body);

            if (mute)
                await webRequestController.SignedFetchPostAsync(mutesUrl, args, string.Empty, ct).WithNoOpAsync();
            else
                await webRequestController.SignedFetchDeleteAsync(mutesUrl, args, string.Empty, ct).WithNoOpAsync();

            ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} {(mute ? "Muted" : "Unmuted")} {walletAddress} via API");
        }
    }
}
