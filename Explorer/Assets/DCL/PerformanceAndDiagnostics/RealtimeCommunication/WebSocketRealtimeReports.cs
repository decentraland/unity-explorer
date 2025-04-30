using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Web3.Identities;
using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;

namespace Assets.DCL.RealtimeCommunication
{
    public class WebSocketRealtimeReports : IRealtimeReports
    {
        private readonly IWeb3IdentityCache web3IdentityCache;
        private ClientWebSocket webSocket;

        public WebSocketRealtimeReports(IWeb3IdentityCache web3IdentityCache)
        {
            this.web3IdentityCache = web3IdentityCache;
            this.webSocket = new ClientWebSocket();

            web3IdentityCache.OnIdentityChanged += ConnectOnChange;
        }

        ~WebSocketRealtimeReports()
        {
            web3IdentityCache.OnIdentityChanged -= ConnectOnChange;
        }

        public bool IsConnected => webSocket.State is WebSocketState.Open;

        private void ConnectOnChange()
        {
            ConnectAsync(CancellationToken.None).Forget();
        }

        public async UniTask ConnectAsync(CancellationToken ct)
        {
            if (IsConnected)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "normal closure", ct);
                webSocket.Abort();
                webSocket.Dispose();
                webSocket = new ClientWebSocket();
            }

            string? address = web3IdentityCache.Identity?.Address.ToString();

            if (address == null)
            {
                ReportHub.LogError(ReportCategory.REALTIME_COMMUNICATION, $"Identity is null, cannot connect to realtime notification websocket, fake identity is used");
                address = "fake";
            }

            var url = $"wss://events-notifier-rpc.decentraland.zone/{address}/notifications";
            await webSocket.ConnectAsync(new Uri(url), ct);
        }

        public void Report(string jsonContent)
        {
            if (IsConnected == false)
            {
                ReportHub.LogError(ReportCategory.REALTIME_COMMUNICATION, $"websocket is not connected: {webSocket.State}");
                return;
            }

            //TODO memory pooling
            byte[] bytes = Encoding.UTF8.GetBytes(jsonContent);

            //TODO proper cancellation
            webSocket.SendAsync(
                          bytes,
                          WebSocketMessageType.Text,
                          true,
                          CancellationToken.None
                      )
                     .AsUniTask()
                     .Forget();
        }
    }
}
