using Cysharp.Threading.Tasks;
using ECS.SceneLifeCycle.Systems;
using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace ECS.SceneLifeCycle.LocalSceneDevelopment
{
    // TODO: Refactor logs for ReportHandler with new tag...
    public class LocalSceneDevelopmentController
    {
        private const int TIMEOUT_SECONDS = 60;
        private const string SCENE_CODE_UPDATE_MESSAGE = "update";
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
            localSceneWebsocketServer = localSceneServer.Replace("http", "ws");
            Debug.Log("LocalSceneDevelopmentController - trying to connect to: " + localSceneWebsocketServer);

            ConnectToServerAsync().Forget();
        }

        private async UniTaskVoid ConnectToServerAsync()
        {
            webSocket = new ClientWebSocket();
            var uri = new Uri(localSceneWebsocketServer);
            await webSocket.ConnectAsync(uri, CancellationToken.None).AsUniTask();
            Debug.Log($"PRAVs - websocket connection state: {webSocket.State}");

            var receiveBuffer = new byte[1024];

            while (webSocket.State == WebSocketState.Open)
            {
                var receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), CancellationToken.None);
                var receivedMessage = Encoding.UTF8.GetString(receiveBuffer, 0, receiveResult.Count);
                Debug.Log($"PRAVs - websocket connection received message: {receivedMessage}");

                if (receivedMessage == SCENE_CODE_UPDATE_MESSAGE)
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
