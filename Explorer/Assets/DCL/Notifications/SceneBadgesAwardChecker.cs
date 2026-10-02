using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using DCL.Web3.Identities;
using DCL.WebRequests;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility;
using Utility.Times;

namespace DCL.Notifications
{
    public class SceneBadgesAwardChecker : ISceneBadgesAwardCheck, IDisposable
    {
        private const string NOTIFICATION_TITLE = "New Badge Unlocked!";
        private const string VISIBLE_STATE = "visible";
        private const string CELEBRATED_BODY = "{\"celebrated\":true}";

        private static readonly TimeSpan REQUEST_THROTTLE = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan LOGIN_CHECK_DELAY = TimeSpan.FromSeconds(5);

        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource urlsSource;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly object requestLock = new ();

        private DateTime lastRequestTime = DateTime.MinValue;
        private int checkRequested;
        private CancellationTokenSource? checkLoopCts;

        public SceneBadgesAwardChecker(IWebRequestController webRequestController, IDecentralandUrlsSource urlsSource, IWeb3IdentityCache web3IdentityCache)
        {
            this.webRequestController = webRequestController;
            this.urlsSource = urlsSource;
            this.web3IdentityCache = web3IdentityCache;

            web3IdentityCache.OnIdentityChanged += StartCheckLoop;
            web3IdentityCache.OnIdentityCleared += StopCheckLoop;

            StartCheckLoop();
        }

        public void Dispose()
        {
            web3IdentityCache.OnIdentityChanged -= StartCheckLoop;
            web3IdentityCache.OnIdentityCleared -= StopCheckLoop;
            StopCheckLoop();
        }

        public void RequestCheck()
        {
            lock (requestLock)
            {
                DateTime now = DateTime.UtcNow;

                if (now - lastRequestTime < REQUEST_THROTTLE)
                    return;

                lastRequestTime = now;
            }

            Interlocked.Exchange(ref checkRequested, 1);
        }

        private void StartCheckLoop()
        {
            checkLoopCts = checkLoopCts.SafeRestart();
            RunCheckLoopAsync(checkLoopCts.Token).SuppressCancellationThrow().Forget();
        }

        private void StopCheckLoop() =>
            checkLoopCts.SafeCancelAndDispose();

        // Single loop per identity: checks never overlap, and hints arriving mid-check coalesce into one follow-up
        private async UniTask RunCheckLoopAsync(CancellationToken ct)
        {
            await UniTask.Delay(LOGIN_CHECK_DELAY, DelayType.Realtime, cancellationToken: ct);
            Interlocked.Exchange(ref checkRequested, 0);

            while (!ct.IsCancellationRequested)
            {
                try { await CheckAwardsAsync(ct); }
                catch (OperationCanceledException) { return; }
                catch (Exception e) { ReportHub.LogException(e, ReportCategory.BADGES); }

                await UniTask.WaitUntil(() => Interlocked.Exchange(ref checkRequested, 0) == 1, cancellationToken: ct);
            }
        }

        private async UniTask CheckAwardsAsync(CancellationToken ct)
        {
            IWeb3Identity? identity = web3IdentityCache.Identity;

            if (identity == null || identity.IsExpired || identity.IsGuest())
                return;

            string address = identity.Address.ToString();
            string baseUrl = $"{urlsSource.Url(DecentralandUrl.Badges)}/users/{address}/scene-badges";
            string mineUrl = $"{baseUrl}/mine";
            ulong unixTimestamp = DateTime.UtcNow.UnixTimeAsMilliseconds();

            SceneBadgesResponse? response = await webRequestController.GetAsync(
                                                                           mineUrl,
                                                                           ct,
                                                                           ReportCategory.BADGES,
                                                                           signInfo: WebRequestSignInfo.NewFromUrl(urlsSource.GetOriginalUrl(mineUrl), unixTimestamp, "get"),
                                                                           headersInfo: new WebRequestHeadersInfo().WithSign(string.Empty, unixTimestamp))
                                                                      .CreateFromNewtonsoftJsonAsync<SceneBadgesResponse>();

            List<SceneBadgeData>? badges = response?.data?.badges;

            if (badges == null)
                return;

            foreach (SceneBadgeData badge in badges)
            {
                SceneBadgeAwardData? award = badge.award;

                if (award == null || award.state != VISIBLE_STATE || !string.IsNullOrEmpty(award.celebratedAt))
                    continue;

                NotificationsBusController.Instance.AddNotification(new BadgeGrantedNotification
                {
                    Id = $"scene-badge-{award.id}",
                    Type = NotificationType.BADGE_GRANTED,
                    Address = address,
                    Timestamp = string.IsNullOrEmpty(badge.completedAt) ? DateTime.UtcNow.UnixTimeAsMilliseconds().ToString() : badge.completedAt,
                    Read = true,
                    Metadata = new BadgeGrantedNotificationMetadata
                    {
                        Id = badge.id,
                        Title = NOTIFICATION_TITLE,
                        Description = badge.name,
                        Image = badge.assets?.image2d?.normal ?? string.Empty,
                    },
                });

                string awardUrl = $"{baseUrl}/{award.id}";
                unixTimestamp = DateTime.UtcNow.UnixTimeAsMilliseconds();

                try
                {
                    await webRequestController.PatchAsync(
                                                   awardUrl,
                                                   GenericPostArguments.CreateJson(CELEBRATED_BODY),
                                                   ct,
                                                   ReportCategory.BADGES,
                                                   signInfo: WebRequestSignInfo.NewFromUrl(urlsSource.GetOriginalUrl(awardUrl), unixTimestamp, "patch"),
                                                   headersInfo: new WebRequestHeadersInfo().WithSign(string.Empty, unixTimestamp))
                                              .WithNoOpAsync();
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception e) { ReportHub.LogException(e, ReportCategory.BADGES); }
            }
        }

        private class SceneBadgesResponse
        {
            public SceneBadgesResponseData? data;
        }

        private class SceneBadgesResponseData
        {
            public List<SceneBadgeData>? badges;
        }

        private class SceneBadgeData
        {
            public string id = string.Empty;
            public string name = string.Empty;
            public string? completedAt;
            public SceneBadgeAssetsData? assets;
            public SceneBadgeAwardData? award;
        }

        private class SceneBadgeAssetsData
        {
            [JsonProperty("2d")]
            public SceneBadgeImageData? image2d;
        }

        private class SceneBadgeImageData
        {
            public string? normal;
        }

        private class SceneBadgeAwardData
        {
            public string id = string.Empty;
            public string? state;
            public string? celebratedAt;
        }
    }
}
