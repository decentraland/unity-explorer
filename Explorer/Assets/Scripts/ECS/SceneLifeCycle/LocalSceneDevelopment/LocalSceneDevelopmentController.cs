using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using ECS.SceneLifeCycle.Systems;
using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;

namespace ECS.SceneLifeCycle.LocalSceneDevelopment
{
    [LogCategory(ReportCategory.SDK_LOCAL_SCENE_DEVELOPMENT)]
    public class LocalSceneDevelopmentController
    {
        private bool initialized = false;
        private ReloadSceneController reloadController;
        private string localSceneWebsocketServer;
        private ClientWebSocket webSocket;

        public LocalSceneDevelopmentController(ReloadSceneController reloadController)
        {
            this.reloadController = reloadController;
        }

        public void Initialize(string localSceneServer)
        {
            if (initialized) return;

            initialized = true;
            localSceneWebsocketServer = localSceneServer.Contains("https") ? localSceneServer.Replace("https", "wss") : localSceneServer.Replace("http", "ws");
            ReportHub.Log(ReportCategory.SDK_LOCAL_SCENE_DEVELOPMENT, $"Trying to connect to: {localSceneWebsocketServer}");

            ConnectToServerAsync().Forget();
        }

        private async UniTaskVoid ConnectToServerAsync()
        {
            webSocket = new ClientWebSocket();
            var uri = new Uri(localSceneWebsocketServer);
            await webSocket.ConnectAsync(uri, CancellationToken.None).AsUniTask();
            ReportHub.Log(ReportCategory.SDK_LOCAL_SCENE_DEVELOPMENT, $"Websocket connection state: {webSocket.State}");

            var receiveBuffer = new byte[1024];

            while (webSocket.State == WebSocketState.Open)
            {
                var receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), CancellationToken.None);
                var receivedMessage = Encoding.UTF8.GetString(receiveBuffer, 0, receiveResult.Count);
                ReportHub.Log(ReportCategory.SDK_LOCAL_SCENE_DEVELOPMENT, $"Websocket connection received message: {receivedMessage}");

                if (!string.IsNullOrEmpty(receivedMessage))
                    await reloadController.TryReloadSceneAsync();
            }
        }

        public void Dispose()
        {
            try { webSocket?.Dispose(); }
            catch (ObjectDisposedException) { }
        }
    }
}
