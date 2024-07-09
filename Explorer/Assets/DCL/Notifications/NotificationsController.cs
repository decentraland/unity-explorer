using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.WebRequests;
using SceneRuntime.Apis.Modules.SignedFetch.Messages;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility.Times;

namespace DCL.Notification
{
    public class NotificationsController
    {
        private const string NOTIFICATION_URL = "https://notifications.decentraland.org/notifications";
        private readonly IWebRequestController webRequestController;
        private readonly CommonArguments commonArguments;
        private ulong unixTimestamp;

        public NotificationsController(IWebRequestController webRequestController)
        {
            this.webRequestController = webRequestController;
            commonArguments = new CommonArguments(new URLBuilder().AppendDomain(URLDomain.FromString(NOTIFICATION_URL)).Build());
            GetNotificationAsync().Forget();
        }

        private async UniTaskVoid GetNotificationAsync()
        {
            await UniTask.Delay(TimeSpan.FromSeconds(5));
            unixTimestamp = DateTime.UtcNow.UnixTimeAsMilliseconds();

            NotificationDTOList notifications = await webRequestController.GetAsync(
                commonArguments,
                new CancellationToken(),
                signInfo: WebRequestSignInfo.NewFromRaw(string.Empty, commonArguments.URL, unixTimestamp, "get"),
                headersInfo: new WebRequestHeadersInfo().WithSign(string.Empty, unixTimestamp)
            ).CreateFromJson<NotificationDTOList>(WRJsonParser.Unity);

            Debug.Log("notifications count " + notifications.notifications.Count);
            foreach (NotificationDTO notificationsElement in notifications.notifications)
            {
                Debug.Log("Notification: " + notificationsElement.id + " " + notificationsElement.type + " " + notificationsElement.read);
            }
            GetNotificationAsync().Forget();
        }
    }
}
