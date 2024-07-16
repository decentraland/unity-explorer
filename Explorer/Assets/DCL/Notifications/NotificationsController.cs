using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Notification.NotificationsBus;
using DCL.Notification.Serialization;
using DCL.Web3.Identities;
using DCL.WebRequests;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Utility;
using Utility.Times;

namespace DCL.Notification
{
    public class NotificationsController : IDisposable
    {
        private const string NOTIFICATION_URL = "https://notifications.decentraland.org/notifications";
        private const string NOTIFICATION_READ_URL = "https://notifications.decentraland.org/notifications/read";

        private static readonly JsonSerializerSettings SERIALIZER_SETTINGS = new () { Converters = new JsonConverter[] { new NotificationJsonDtoConverter() } };

        private readonly CancellationTokenSource cancellationToken;
        private readonly IWebRequestController webRequestController;
        private readonly INotificationsBusController notificationsBusController;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly CommonArguments commonArgumentsForSetRead;
        private readonly StringBuilder bodyBuilder = new ();
        private readonly URLParameter onlyUnreadParameter = new ("onlyUnread", "true");
        private readonly URLBuilder urlBuilder = new();
        private CommonArguments commonArguments;
        private ulong unixTimestamp;
        private ulong lastPolledTimestamp;

        public NotificationsController(IWebRequestController webRequestController, INotificationsBusController notificationsBusController, IWeb3IdentityCache web3IdentityCache)
        {
            this.webRequestController = webRequestController;
            this.notificationsBusController = notificationsBusController;
            this.web3IdentityCache = web3IdentityCache;

            cancellationToken = new CancellationTokenSource();
            lastPolledTimestamp = DateTime.UtcNow.UnixTimeAsMilliseconds();
            commonArgumentsForSetRead = new CommonArguments(new URLBuilder().AppendDomain(URLDomain.FromString(NOTIFICATION_READ_URL)).Build());

            GetNewNotificationAsync().SuppressCancellationThrow().Forget();
        }

        private async UniTask GetNewNotificationAsync()
        {
            do
            {
                await UniTask.Delay(TimeSpan.FromSeconds(5), DelayType.Realtime, cancellationToken: cancellationToken.Token);

                if(web3IdentityCache.Identity == null)
                    continue;

                urlBuilder.Clear();
                urlBuilder.AppendDomain(URLDomain.FromString(NOTIFICATION_URL))
                          .AppendParameter(onlyUnreadParameter)
                          .AppendParameter(new URLParameter("from", lastPolledTimestamp.ToString()));
                commonArguments = new CommonArguments(urlBuilder.Build());

                unixTimestamp = DateTime.UtcNow.UnixTimeAsMilliseconds();
                lastPolledTimestamp = DateTime.UtcNow.UnixTimeAsMilliseconds();
                List<INotification> notifications =
                    await webRequestController.GetAsync(
                        commonArguments,
                        cancellationToken.Token,
                        signInfo: WebRequestSignInfo.NewFromRaw(string.Empty, commonArguments.URL, unixTimestamp, "get"),
                        headersInfo: new WebRequestHeadersInfo().WithSign(string.Empty, unixTimestamp))
                                              .CreateFromNewtonsoftJsonAsync<List<INotification>>(serializerSettings: SERIALIZER_SETTINGS);

                foreach (INotification notification in notifications)
                {
                    notificationsBusController.AddNotification(notification);
                    SetNotificationAsRead(notification.Id);
                }

            }
            while (cancellationToken.IsCancellationRequested == false);
        }

        public void SetNotificationAsRead(string notificationId)
        {
            bodyBuilder.Clear();
            bodyBuilder.Append("{\"notificationIds\":[\"")
                       .Append(notificationId)
                       .Append("\"]}");

            unixTimestamp = DateTime.UtcNow.UnixTimeAsMilliseconds();

            webRequestController.PutAsync(
                commonArgumentsForSetRead,
                WebRequests.GenericPutArguments.CreateJson(bodyBuilder.ToString()),
                new CancellationToken(),
                signInfo: WebRequestSignInfo.NewFromRaw(string.Empty, commonArgumentsForSetRead.URL, unixTimestamp, "put"),
                headersInfo: new WebRequestHeadersInfo().WithSign(string.Empty, unixTimestamp));
        }

        public void Dispose()
        {
            cancellationToken.SafeCancelAndDispose();
        }
    }
}
