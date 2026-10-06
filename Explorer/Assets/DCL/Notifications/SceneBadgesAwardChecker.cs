using Cysharp.Threading.Tasks;
using DCL.BadgesAPIService;
using DCL.Diagnostics;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using DCL.Web3.Identities;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using Utility;
using Utility.Times;

namespace DCL.Notifications
{
    /// <summary>
    ///     Toasts the player's scene badge awards once each. A check runs after login and on every
    ///     <see cref="RequestCheck" /> hint from the current scene; hints that arrive during a check or
    ///     within the throttle merge into one follow-up check, none is dropped.
    /// </summary>
    public class SceneBadgesAwardChecker : ISceneBadgesAwardChecker, IDisposable
    {
        private const string NOTIFICATION_TITLE = "New Badge Unlocked!";
        private const string VISIBLE_STATE = "visible";

        /// <summary>Keeps scene-award toast ids apart from notifications-service ids.</summary>
        private const string NOTIFICATION_ID_PREFIX = "scene-badge-";

        /// <summary>Award ids are UUIDs (badges service). Any other id is refused before it goes into a signed request path.</summary>
        private static readonly Regex AWARD_ID_FORMAT = new ("^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$", RegexOptions.Compiled);

        /// <summary>Minimum time between two award checks.</summary>
        private static readonly TimeSpan REQUEST_THROTTLE = TimeSpan.FromSeconds(3);

        /// <summary>Delay before the post-login check.</summary>
        private static readonly TimeSpan LOGIN_CHECK_DELAY = TimeSpan.FromSeconds(5);

        private readonly BadgesAPIClient badgesApiClient;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly Channel<bool> checkHints = Channel.CreateSingleConsumerUnbounded<bool>();

        private CancellationTokenSource? checkLoopCts;

        public SceneBadgesAwardChecker(BadgesAPIClient badgesApiClient, IWeb3IdentityCache web3IdentityCache)
        {
            this.badgesApiClient = badgesApiClient;
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
            checkHints.Writer.TryComplete();
        }

        public void RequestCheck() =>
            checkHints.Writer.TryWrite(true);

        private void StartCheckLoop()
        {
            checkLoopCts = checkLoopCts.SafeRestart();
            RunCheckLoopAsync(checkLoopCts.Token).SuppressCancellationThrow().Forget();
        }

        private void StopCheckLoop() =>
            checkLoopCts.SafeCancelAndDispose();

        private async UniTask RunCheckLoopAsync(CancellationToken ct)
        {
            await UniTask.Delay(LOGIN_CHECK_DELAY, DelayType.Realtime, cancellationToken: ct);

            while (!ct.IsCancellationRequested)
            {
                while (checkHints.Reader.TryRead(out _)) { }

                try { await CheckAwardsAsync(ct); }
                catch (OperationCanceledException) { return; }
                catch (Exception e) { ReportHub.LogException(e, ReportCategory.BADGES); }

                await UniTask.Delay(REQUEST_THROTTLE, DelayType.Realtime, cancellationToken: ct);
                await checkHints.Reader.WaitToReadAsync(ct);
            }
        }

        private async UniTask CheckAwardsAsync(CancellationToken ct)
        {
            IWeb3Identity? identity = web3IdentityCache.Identity;

            if (identity == null || identity.IsExpired || identity.IsGuest())
                return;

            string address = identity.Address.ToString();
            SceneBadgesResponse? response = await badgesApiClient.FetchOwnSceneBadgesAsync(address, ct);
            List<SceneBadgeData>? badges = response?.data?.badges;

            if (badges == null)
                return;

            foreach (SceneBadgeData badge in badges)
            {
                SceneBadgeAwardData? award = badge.award;

                if (award == null || award.state != VISIBLE_STATE || award.celebratedAt != null || !AWARD_ID_FORMAT.IsMatch(award.id))
                    continue;

                // Mark first: if the PATCH fails the award stays uncelebrated and the next check retries,
                // instead of toasting the same award on every check.
                try { await badgesApiClient.MarkSceneBadgeCelebratedAsync(address, award.id, ct); }
                catch (OperationCanceledException) { throw; }
                catch (Exception e)
                {
                    ReportHub.LogException(e, ReportCategory.BADGES);
                    continue;
                }

                NotificationsBusController.Instance.AddNotification(new BadgeGrantedNotification
                {
                    Id = NOTIFICATION_ID_PREFIX + award.id,
                    Type = NotificationType.BADGE_GRANTED,
                    Address = address,
                    Timestamp = string.IsNullOrEmpty(badge.completedAt) ? DateTime.UtcNow.UnixTimeAsMilliseconds().ToString() : badge.completedAt,
                    Read = true,
                    Metadata = new BadgeGrantedNotificationMetadata
                    {
                        Id = badge.id,
                        Title = NOTIFICATION_TITLE,
                        Description = badge.name,
                        Image = badge.assets?.textures2d?.normal ?? string.Empty,
                    },
                });
            }
        }
    }
}
