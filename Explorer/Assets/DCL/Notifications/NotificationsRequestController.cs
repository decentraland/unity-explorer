using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Infrastructure.Utility.Types;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Notifications.Serialization;
using DCL.NotificationsBusController.NotificationsBus;
using DCL.NotificationsBusController.NotificationTypes;
using DCL.Optimization.ThreadSafePool;
using DCL.Web3.Identities;
using DCL.WebRequests;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Utility.Times;

namespace DCL.Notifications
{
    public class NotificationsRequestController
    {
        private static readonly TimeSpan NOTIFICATIONS_DELAY = TimeSpan.FromSeconds(5);

        private readonly JsonSerializerSettings serializerSettings;
        private readonly IWebRequestController webRequestController;
        private readonly INotificationsBusController notificationsBusController;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly CommonArguments commonArgumentsForSetRead;
        private readonly StringBuilder bodyBuilder = new ();
        private readonly URLParameter onlyUnreadParameter = new ("onlyUnread", "true");
        private readonly URLParameter limitParameter = new ("limit", "50");
        private readonly URLBuilder urlBuilder = new ();
        private CommonArguments commonArguments;
        private ulong unixTimestamp;
        private ulong lastPolledTimestamp;

        public NotificationsRequestController(
            IWebRequestController webRequestController,
            INotificationsBusController notificationsBusController,
            IDecentralandUrlsSource decentralandUrlsSource,
            IWeb3IdentityCache web3IdentityCache,
            bool includeFriendsNotifications
        )
        {
            this.webRequestController = webRequestController;
            this.notificationsBusController = notificationsBusController;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.web3IdentityCache = web3IdentityCache;

            serializerSettings = new () { Converters = new JsonConverter[] { new NotificationJsonDtoConverter(includeFriendsNotifications) } };

            lastPolledTimestamp = DateTime.UtcNow.UnixTimeAsMilliseconds();

            commonArgumentsForSetRead = new CommonArguments(
                new URLBuilder()
                   .AppendDomain(
                        URLDomain.FromString(
                            decentralandUrlsSource.Url(DecentralandUrl.NotificationRead)
                        )
                    )
                   .Build()
            );
        }

        public async UniTask<List<INotification>> GetMostRecentNotificationsAsync(CancellationToken ct)
        {
            do await UniTask.Delay(NOTIFICATIONS_DELAY, DelayType.Realtime, cancellationToken: ct);
            while (web3IdentityCache.Identity == null || web3IdentityCache.Identity.IsExpired);

            urlBuilder.Clear();

            urlBuilder.AppendDomain(URLDomain.FromString(decentralandUrlsSource.Url(DecentralandUrl.Notification)))
                      .AppendParameter(limitParameter);

            commonArguments = new CommonArguments(urlBuilder.Build());
            unixTimestamp = DateTime.UtcNow.UnixTimeAsMilliseconds();

            List<INotification> notifications =
                await webRequestController.GetAsync(
                                               commonArguments,
                                               ct,
                                               ReportCategory.UI,
                                               signInfo: WebRequestSignInfo.NewFromUrl(commonArguments.URL, unixTimestamp, "get"),
                                               headersInfo: new WebRequestHeadersInfo().WithSign(string.Empty, unixTimestamp))
                                          .CreateFromNewtonsoftJsonAsync<List<INotification>>(serializerSettings: serializerSettings);

            return notifications;
        }

        public async UniTask StartGettingNewNotificationsOverTimeAsync(CancellationToken ct)
        {
            do
            {
                await UniTask.Delay(NOTIFICATIONS_DELAY, DelayType.Realtime, cancellationToken: ct);

                if (web3IdentityCache.Identity == null || web3IdentityCache.Identity.IsExpired)
                    continue;

                urlBuilder.Clear();

                urlBuilder.AppendDomain(URLDomain.FromString(decentralandUrlsSource.Url(DecentralandUrl.Notification)))
                          .AppendParameter(onlyUnreadParameter)
                          .AppendParameter(new URLParameter("from", lastPolledTimestamp.ToString()));

                commonArguments = new CommonArguments(urlBuilder.Build());

                unixTimestamp = DateTime.UtcNow.UnixTimeAsMilliseconds();

                // TODO remove allocation of List on serialization
                List<INotification> notifications =
                    await webRequestController.GetAsync(
                                                   commonArguments,
                                                   ct,
                                                   ReportCategory.UI,
                                                   signInfo: WebRequestSignInfo.NewFromUrl(commonArguments.URL, unixTimestamp, "get"),
                                                   headersInfo: new WebRequestHeadersInfo().WithSign(string.Empty, unixTimestamp))
                                              .CreateFromNewtonsoftJsonAsync<List<INotification>>(serializerSettings: serializerSettings);

                if (notifications.Count == 0)
                    continue;

                lastPolledTimestamp = DateTime.UtcNow.UnixTimeAsMilliseconds();


                using var scope = ThreadSafeListPool<string>.SHARED.Get(out var list);
                foreach (INotification notification in notifications)
                    try
                    {
                        notificationsBusController.AddNotification(notification);
                        list.Add(notification.Id);
                    }
                    catch (Exception e)
                    {
                        ReportHub.LogException(e, ReportCategory.UI);
                    }

                await SetNotificationAsReadAsync(list.AsReadOnlyList(), ct);
            }
            while (ct.IsCancellationRequested == false);
        }

        public async UniTask SetNotificationAsReadAsync(string notificationId, CancellationToken ct)
        {
            using var scope = ThreadSafeListPool<string>.SHARED.Get(out var list);
            list.Add(notificationId);
            await SetNotificationAsReadAsync(list.AsReadOnlyList(), ct);
        }

        private async UniTask SetNotificationAsReadAsync(ReadOnlyList<string> notificationIds, CancellationToken ct)
        {
            if (web3IdentityCache.Identity == null || web3IdentityCache.Identity.IsExpired) return;

            if (notificationIds.Count == 0) return;

            bodyBuilder.Clear();
            bodyBuilder.Append("{\"notificationIds\":[");

            var first = true;
            foreach (string id in notificationIds)
            {
                if (!first)
                    bodyBuilder.Append(',');
                bodyBuilder.Append('\"').Append(id).Append('\"');
                first = false;
            }

            bodyBuilder.Append("]}");

            unixTimestamp = DateTime.UtcNow.UnixTimeAsMilliseconds();

            await webRequestController.PutAsync(
                                           commonArgumentsForSetRead,
                                           GenericPutArguments.CreateJson(bodyBuilder.ToString()),
                                           ct,
                                           ReportCategory.UI,
                                           signInfo: WebRequestSignInfo.NewFromUrl(commonArgumentsForSetRead.URL, unixTimestamp, "put"),
                                           headersInfo: new WebRequestHeadersInfo().WithSign(string.Empty, unixTimestamp))
                                      .WithNoOpAsync();
        }
    }
}
