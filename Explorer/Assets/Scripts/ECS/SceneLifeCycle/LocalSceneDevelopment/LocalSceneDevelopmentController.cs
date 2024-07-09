using Cysharp.Threading.Tasks;
using ECS.SceneLifeCycle.Systems;
using Newtonsoft.Json;
using SocketIOClient;
using SocketIOClient.Newtonsoft.Json;
using SocketIOClient.Transport;
using System;
using UnityEngine;

namespace ECS.SceneLifeCycle.LocalSceneDevelopment
{
    public class LocalSceneDevelopmentController
    {
        private const int TIMEOUT_SECONDS = 60;
        private bool initialized = false;
        private ReloadSceneController reloadController;
        private string localSceneServer;
        private SocketIO? webSocket;

        public LocalSceneDevelopmentController(ReloadSceneController reloadController)
        {
            this.reloadController = reloadController;
        }

        public void Initialize(string localSceneServer)
        {
            if (initialized) return;

            initialized = true;
            this.localSceneServer = localSceneServer.Replace("http", "ws");
            Debug.Log("PRAVS - trying to connect to: " + this.localSceneServer);

            // Start websocket transport...
            // try
            // {
                // await ConnectToServerAsync();
            // }
            // finally
            // {
            //     await DisconnectFromServerAsync();
            //     await UniTask.SwitchToMainThread(ct);
            // }

            // ConnectToServerAsync().Forget();
            UniTask.Void(ConnectToServerAsync);
        }

        private async UniTaskVoid ConnectToServerAsync()
        {
            webSocket = InitializeWebSocket();
            // await webSocket.ConnectAsync().AsUniTask().Timeout(TimeSpan.FromSeconds(TIMEOUT_SECONDS));
            // Debug.Log("PRAVS - connected to local scene websocke server??? " + webSocket.Connected);
            await webSocket.ConnectAsync().AsUniTask();
        }

        private SocketIO InitializeWebSocket()
        {
            if (webSocket != null) return webSocket;

            var uri = new Uri(localSceneServer);

            webSocket = new SocketIO(uri, new SocketIOOptions
            {
                Path = null,
                ConnectionTimeout = default,
                Query = null,
                Reconnection = true,
                ReconnectionDelay = 0,
                ReconnectionDelayMax = 0,
                ReconnectionAttempts = 10,
                RandomizationFactor = 0,
                ExtraHeaders = null,
                Transport = TransportProtocol.WebSocket,
                EIO = (EngineIO)0,
                AutoUpgrade = false,
                Auth = null,
                Proxy = null
            });

            webSocket.JsonSerializer = new NewtonsoftJsonSerializer(new JsonSerializerSettings());

            // webSocket.On("outcome", ProcessSignatureOutcomeMessage);
            webSocket.On("open", (response) =>
            {
                Debug.Log("PRAVS - WS connection OPEN");
            });
            webSocket.On("error", (response) =>
            {
                Debug.Log("PRAVS - WS connection ERROR");
            });
            webSocket.On("connect", (response) =>
            {
                Debug.Log("PRAVS - WS connection CONNECT");
            });
            webSocket.On("message", (response) =>
            {
                Debug.Log("PRAVS - WS connection MESSAGE " + response.GetValue<string>());
            });

            return webSocket;
        }

        private async UniTask DisconnectFromServerAsync()
        {
            if (webSocket is { Connected: true })
                await webSocket.DisconnectAsync();
        }

        public void Dispose()
        {
            try { webSocket?.Dispose(); }
            catch (ObjectDisposedException) { }
        }
    }
}
