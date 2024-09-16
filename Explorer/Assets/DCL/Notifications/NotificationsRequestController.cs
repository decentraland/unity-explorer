using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Notifications.Serialization;
using DCL.NotificationsBusController.NotificationsBus;
using DCL.NotificationsBusController.NotificationTypes;
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
        private static readonly JsonSerializerSettings SERIALIZER_SETTINGS = new () { Converters = new JsonConverter[] { new NotificationJsonDtoConverter() } };
        private static readonly TimeSpan NOTIFICATIONS_DELAY = TimeSpan.FromSeconds(5);

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
            IWeb3IdentityCache web3IdentityCache
        )
        {
            this.webRequestController = webRequestController;
            this.notificationsBusController = notificationsBusController;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.web3IdentityCache = web3IdentityCache;

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
                                               signInfo: WebRequestSignInfo.NewFromRaw(string.Empty, commonArguments.URL, unixTimestamp, "get"),
                                               headersInfo: new WebRequestHeadersInfo().WithSign(string.Empty, unixTimestamp))
                                          .CreateFromNewtonsoftJsonAsync<List<INotification>>(serializerSettings: SERIALIZER_SETTINGS);

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

                List<INotification> notifications =
                    await webRequestController.GetAsync(
                                                   commonArguments,
                                                   ct,
                                                   ReportCategory.UI,
                                                   signInfo: WebRequestSignInfo.NewFromRaw(string.Empty, commonArguments.URL, unixTimestamp, "get"),
                                                   headersInfo: new WebRequestHeadersInfo().WithSign(string.Empty, unixTimestamp))
                                              .CreateFromNewtonsoftJsonAsync<List<INotification>>(serializerSettings: SERIALIZER_SETTINGS);

                if (notifications.Count > 0)
                    lastPolledTimestamp = DateTime.UtcNow.UnixTimeAsMilliseconds();

                await UniTask.WhenAll(notifications.Select(notification =>
                {
                    notificationsBusController.AddNotification(notification);
                    return SetNotificationAsReadAsync(notification.Id, ct);
                }));
            }
            while (ct.IsCancellationRequested == false);
        }

        public async UniTask SetNotificationAsReadAsync(string notificationId, CancellationToken ct)
        {
            if (web3IdentityCache.Identity == null || web3IdentityCache.Identity.IsExpired) return;

            bodyBuilder.Clear();

            bodyBuilder.Append("{\"notificationIds\":[\"")
                       .Append(notificationId)
                       .Append("\"]}");

            unixTimestamp = DateTime.UtcNow.UnixTimeAsMilliseconds();

            await webRequestController.PutAsync(
                                           commonArgumentsForSetRead,
                                           GenericPutArguments.CreateJson(bodyBuilder.ToString()),
                                           ct,
                                           ReportCategory.UI,
                                           signInfo: WebRequestSignInfo.NewFromRaw(string.Empty, commonArgumentsForSetRead.URL, unixTimestamp, "put"),
                                           headersInfo: new WebRequestHeadersInfo().WithSign(string.Empty, unixTimestamp))
                                      .WithNoOpAsync();
        }
    }
}
