using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.MCP.Handlers;
using Fleck;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

namespace DCL.MCP
{
    /// <summary>
    ///     WebSocket сервер для MCP интеграции на базе Fleck.
    ///     Позволяет внешним инструментам (Claude Desktop, MCP Server) подключаться к Unity runtime
    ///     и запрашивать информацию о состоянии приложения (FPS, память, сцены и т.д.)
    /// </summary>
    public class MCPWebSocketServer : IDisposable
    {
        private const int DEFAULT_PORT = 7777;

        private readonly WebSocketServer server;
        private readonly List<IWebSocketConnection> clients = new ();
        private readonly Dictionary<string, Func<JObject, UniTask<object>>> commandHandlers = new ();

        private bool isRunning;

        public MCPWebSocketServer(int port = DEFAULT_PORT)
        {
            // Отключаем логи Fleck (используем наш ReportHub)
            FleckLog.Level = LogLevel.Error;

            FleckLog.LogAction = (level, message, ex) =>
            {
                if (level >= LogLevel.Error)
                    ReportHub.LogError(ReportCategory.DEBUG, $"[MCP WS] Fleck Error: {message} {ex}");
            };

            server = new WebSocketServer($"ws://0.0.0.0:{port}");
            RegisterDefaultHandlers();
        }

        private void RegisterDefaultHandlers()
        {
            RegisterHandler("ping", HandlePing);
            RegisterHandler("getFPS", HandleGetFPS);
            RegisterHandler("getSceneInfo", HandleGetSceneInfo);
            RegisterHandler("getMemoryUsage", HandleGetMemoryUsage);
            RegisterHandler("getSystemInfo", HandleGetSystemInfo);
        }

        /// <summary>
        ///     Регистрирует кастомный обработчик команды
        /// </summary>
        public void RegisterHandler(string method, Func<JObject, UniTask<object>> handler) =>
            commandHandlers[method] = handler;

        /// <summary>
        ///     Запускает WebSocket сервер
        /// </summary>
        public void Start()
        {
            if (isRunning) return;

            try
            {
                server.Start(socket =>
                {
                    socket.OnOpen = () => OnClientConnected(socket);
                    socket.OnClose = () => OnClientDisconnected(socket);
                    socket.OnMessage = message => OnMessageReceived(socket, message).Forget();
                    socket.OnError = ex => OnError(socket, ex);
                });

                isRunning = true;
                ReportHub.Log(ReportCategory.DEBUG, $"[MCP WS] Server started on ws://0.0.0.0:{DEFAULT_PORT}");

                // Attach server instance for handlers that want to broadcast events
                MCPChatHandler.AttachServer(this);
            }
            catch (Exception e)
            {
                ReportHub.LogError(ReportCategory.DEBUG, $"[MCP WS] Failed to start server: {e}");
                throw;
            }
        }

        private void OnClientConnected(IWebSocketConnection socket)
        {
            clients.Add(socket);
            ReportHub.Log(ReportCategory.DEBUG, $"[MCP WS] Client connected: {socket.ConnectionInfo.ClientIpAddress}:{socket.ConnectionInfo.ClientPort}");
        }

        private void OnClientDisconnected(IWebSocketConnection socket)
        {
            clients.Remove(socket);
            ReportHub.Log(ReportCategory.DEBUG, $"[MCP WS] Client disconnected: {socket.ConnectionInfo.ClientIpAddress}:{socket.ConnectionInfo.ClientPort}");
        }

        private void OnError(IWebSocketConnection socket, Exception ex) =>
            ReportHub.LogError(ReportCategory.DEBUG, $"[MCP WS] WebSocket error from {socket.ConnectionInfo.ClientIpAddress}: {ex.Message}");

        private async UniTaskVoid OnMessageReceived(IWebSocketConnection socket, string message)
        {
            try
            {
                var request = JObject.Parse(message);
                JToken id = request["id"];
                string method = request["method"]?.ToString();
                var parameters = request["params"] as JObject;

                ReportHub.Log(ReportCategory.DEBUG, $"[MCP WS] Received: method={method}, id={id}");

                if (string.IsNullOrEmpty(method))
                {
                    await SendErrorAsync(socket, id, -32600, "Invalid request: missing method");
                    return;
                }

                if (commandHandlers.TryGetValue(method, out Func<JObject, UniTask<object>> handler))
                {
                    // Переключаемся на main thread для обработчиков, которым нужен Unity API
                    await UniTask.SwitchToMainThread();

                    object result = await handler(parameters ?? new JObject());
                    await SendResponseAsync(socket, id, result);
                }
                else
                    await SendErrorAsync(socket, id, -32601, $"Method not found: {method}");
            }
            catch (JsonException e)
            {
                ReportHub.LogError(ReportCategory.DEBUG, $"[MCP WS] JSON parse error: {e.Message}");
                await SendErrorAsync(socket, null, -32700, $"Parse error: {e.Message}");
            }
            catch (Exception e)
            {
                ReportHub.LogError(ReportCategory.DEBUG, $"[MCP WS] Error processing message: {e}");
                await SendErrorAsync(socket, null, -32603, $"Internal error: {e.Message}");
            }
        }

        private async UniTask SendResponseAsync(IWebSocketConnection socket, JToken id, object result)
        {
            var response = new JObject
            {
                ["id"] = id,
                ["result"] = JToken.FromObject(result),
            };

            await SendJsonAsync(socket, response);
            ReportHub.Log(ReportCategory.DEBUG, $"[MCP WS] Sent response: id={id}");
        }

        private async UniTask SendErrorAsync(IWebSocketConnection socket, JToken id, int code, string message)
        {
            var response = new JObject
            {
                ["id"] = id,
                ["error"] = new JObject
                {
                    ["code"] = code,
                    ["message"] = message,
                },
            };

            await SendJsonAsync(socket, response);
            ReportHub.LogError(ReportCategory.DEBUG, $"[MCP WS] Sent error: id={id}, code={code}, message={message}");
        }

        private async UniTask SendJsonAsync(IWebSocketConnection socket, JObject json)
        {
            if (!socket.IsAvailable) return;

            string jsonStr = json.ToString(Formatting.None);
            await UniTask.RunOnThreadPool(() => socket.Send(jsonStr));
        }

        /// <summary>
        ///     Broadcast события всем подключённым клиентам
        /// </summary>
        public async UniTask BroadcastEventAsync(string eventName, object data)
        {
            var eventMessage = new JObject
            {
                ["event"] = eventName,
                ["data"] = JToken.FromObject(data),
            };

            string jsonStr = eventMessage.ToString(Formatting.None);

            await UniTask.RunOnThreadPool(() =>
            {
                foreach (IWebSocketConnection client in clients.Where(c => c.IsAvailable))
                    try { client.Send(jsonStr); }
                    catch (Exception e) { ReportHub.LogError(ReportCategory.DEBUG, $"[MCP WS] Error broadcasting to client: {e.Message}"); }
            });
        }

        // ==================== Обработчики команд ====================

        private async UniTask<object> HandlePing(JObject parameters) =>
            new
            {
                pong = true,
                timestamp = DateTime.UtcNow.ToString("o"),
                unityTime = UnityEngine.Time.realtimeSinceStartup.ToString("o"),
            };

        private async UniTask<object> HandleGetFPS(JObject parameters)
        {
            // Вычисляем FPS несколькими способами
            float instantFps = 1.0f / UnityEngine.Time.deltaTime;
            float smoothedFps = 1.0f / UnityEngine.Time.smoothDeltaTime;
            float frameTime = UnityEngine.Time.deltaTime * 1000f;

            return new
            {
                fps = Math.Round(instantFps, 2),
                smoothedFps = Math.Round(smoothedFps, 2),
                frameTime = Math.Round(frameTime, 2),
                Application.targetFrameRate,
                vsyncCount = QualitySettings.vSyncCount,
                timestamp = DateTime.UtcNow.ToString("o"),
            };
        }

        private async UniTask<object> HandleGetSceneInfo(JObject parameters)
        {
            Scene activeScene = SceneManager.GetActiveScene();

            var loadedScenes = new List<object>();

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);

                loadedScenes.Add(new
                {
                    scene.name,
                    scene.path,
                    scene.isLoaded,
                    scene.buildIndex,
                });
            }

            return new
            {
                activeScene = new
                {
                    activeScene.name,
                    activeScene.path,
                    activeScene.isLoaded,
                    activeScene.buildIndex,
                    activeScene.rootCount,
                },
                loadedScenes,
                totalScenesCount = SceneManager.sceneCount,
            };
        }

        private async UniTask<object> HandleGetMemoryUsage(JObject parameters) =>
            new
            {
                totalReservedMemoryMB = Math.Round(Profiler.GetTotalReservedMemoryLong() / (1024f * 1024f), 2),
                totalAllocatedMemoryMB = Math.Round(Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f), 2),
                totalUnusedReservedMemoryMB = Math.Round(Profiler.GetTotalUnusedReservedMemoryLong() / (1024f * 1024f), 2),
                monoHeapSizeMB = Math.Round(Profiler.GetMonoHeapSizeLong() / (1024f * 1024f), 2),
                monoUsedSizeMB = Math.Round(Profiler.GetMonoUsedSizeLong() / (1024f * 1024f), 2),
                timestamp = DateTime.UtcNow.ToString("o"),
            };

        private async UniTask<object> HandleGetSystemInfo(JObject parameters) =>
            new
            {
                SystemInfo.operatingSystem,
                SystemInfo.processorType,
                SystemInfo.processorCount,
                SystemInfo.systemMemorySize,
                SystemInfo.graphicsDeviceName,
                graphicsDeviceType = SystemInfo.graphicsDeviceType.ToString(),
                SystemInfo.graphicsMemorySize,
                SystemInfo.maxTextureSize,
                SystemInfo.deviceModel,
                SystemInfo.deviceName,
                Application.unityVersion,
                platform = Application.platform.ToString(),
                timestamp = DateTime.UtcNow.ToString("o"),
            };

        public void Dispose()
        {
            isRunning = false;

            ReportHub.Log(ReportCategory.DEBUG, "[MCP WS] Shutting down server...");

            foreach (IWebSocketConnection client in clients.ToList())
                try { client.Close(); }
                catch
                { /* ignore */
                }

            clients.Clear();
            server?.Dispose();

            ReportHub.Log(ReportCategory.DEBUG, "[MCP WS] Server stopped");
        }
    }
}
