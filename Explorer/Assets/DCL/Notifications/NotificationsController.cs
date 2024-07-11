using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Notification.NotificationsBus;
using DCL.Notification.Serialization;
using DCL.WebRequests;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;
using Utility.Times;

namespace DCL.Notification
{
    public class NotificationsController : IDisposable
    {
        private static readonly JsonSerializerSettings SERIALIZER_SETTINGS = new () { Converters = new JsonConverter[] { new NotificationJsonDtoConverter() } };
        private const string NOTIFICATION_URL = "https://notifications.decentraland.zone/notifications";
        private readonly CancellationTokenSource cancellationToken;
        private readonly IWebRequestController webRequestController;
        private readonly INotificationsBusController notificationsBusController;
        private readonly CommonArguments commonArguments;
        private readonly DateTimeOffset unixEpoch;
        private ulong unixTimestamp;

        public NotificationsController(IWebRequestController webRequestController, INotificationsBusController notificationsBusController)
        {
            this.webRequestController = webRequestController;
            this.notificationsBusController = notificationsBusController;

            unixEpoch = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);
            commonArguments = new CommonArguments(new URLBuilder().AppendDomain(URLDomain.FromString(NOTIFICATION_URL)).AppendParameter(new URLParameter("onlyUnread", "true")).Build());
            cancellationToken = new CancellationTokenSource();
            GetNewNotificationAsync().Forget();
        }

        private async UniTaskVoid GetNewNotificationAsync()
        {
            do
            {
                await UniTask.Delay(TimeSpan.FromSeconds(5));
                unixTimestamp = DateTime.UtcNow.UnixTimeAsMilliseconds();

                List<INotification> notifications =
                    await webRequestController.GetAsync(
                        commonArguments,
                        new CancellationToken(),
                        signInfo: WebRequestSignInfo.NewFromRaw(string.Empty, commonArguments.URL, unixTimestamp, "get"),
                        headersInfo: new WebRequestHeadersInfo().WithSign(string.Empty, unixTimestamp))
                                              .CreateFromNewtonsoftJsonAsync<List<INotification>>(serializerSettings: SERIALIZER_SETTINGS);

                Debug.Log("notification count is " + notifications.Count);
                foreach (INotification notification in notifications)
                {
                    // Convert to Unix time in milliseconds
                    var unixTimeMilliseconds = (ulong)unixEpoch.AddMilliseconds(long.Parse(notification.Timestamp)).ToUnixTimeMilliseconds();

                    if (unixTimestamp - unixTimeMilliseconds <= 5000)
                    {
                        notificationsBusController.AddNotification(notification);
                    }
                }

            }
            while (cancellationToken.IsCancellationRequested == false);
        }

        public void Dispose()
        {
            cancellationToken.SafeCancelAndDispose();
        }
    }
}
