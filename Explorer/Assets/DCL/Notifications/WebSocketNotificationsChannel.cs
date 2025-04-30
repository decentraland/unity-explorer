using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.NotificationsBusController.NotificationsBus;
using DCL.NotificationsBusController.NotificationTypes;
using DCL.Web3.Identities;
using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;

namespace DCL.Notifications
{
    public class WebSocketNotificationsChannel
    {
        private readonly INotificationsBusController notificationsBusController;
        private readonly IWeb3IdentityCache identityCache;
        private readonly ClientWebSocket webSocket;
        private readonly byte[] buffer = new byte[1024 * 16]; // 16 kb

        public WebSocketNotificationsChannel(INotificationsBusController notificationsBusController, IWeb3IdentityCache identityCache)
        {
            this.notificationsBusController = notificationsBusController;
            this.identityCache = identityCache;
            webSocket = new ClientWebSocket();
        }

        public async UniTask StartAsync(CancellationToken ct)
        {
            while (identityCache.Identity == null)
                await UniTask.Delay(TimeSpan.FromMilliseconds(200));

            var address = identityCache.Identity!.Address.ToString();
            var url = $"wss://notifications-processor-rpc.decentraland.zone/{address}/notifications";
            await webSocket.ConnectAsync(new Uri(url), ct);

            while (ct.IsCancellationRequested == false)
            {
                var result = await webSocket.ReceiveAsync(buffer, ct);

                switch (result.MessageType)
                {
                    case WebSocketMessageType.Close:
                        ReportHub.LogError(ReportCategory.REALTIME_COMMUNICATION, "WebSocket was closed by remote server");
                        return;
                    case WebSocketMessageType.Binary:
                        ReportHub.LogError(ReportCategory.REALTIME_COMMUNICATION, "WebSocket binary format is unsupported for this schema");
                        continue;
                    case WebSocketMessageType.Text:
                        string message = Encoding.UTF8.GetString(buffer, 0, result.Count);

                        //TODO message generalized parsing with all schemes
                        ReportHub.Log(ReportCategory.REALTIME_COMMUNICATION, $"Received notification: {message}");
                        notificationsBusController.AddNotification(new MoveToParcelNotification());
                        break;
                    default: throw new ArgumentOutOfRangeException();
                }
            }
        }
    }
}
