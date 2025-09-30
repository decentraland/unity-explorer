using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using System;
using UnityEngine;

namespace DCL.MCP
{
    /// <summary>
    ///     Автоматически запускает MCP WebSocket Server при старте приложения.
    ///     Добавьте этот компонент на GameObject в первой сцене или используйте DontDestroyOnLoad.
    /// </summary>
    public class MCPServerBootstrap : MonoBehaviour
    {
        [SerializeField] private bool startOnAwake = true;
        [SerializeField] private int port = 7777;

        private MCPWebSocketServer server;

        private void Awake()
        {
            // Singleton pattern - только один экземпляр
            if (FindObjectsOfType<MCPServerBootstrap>().Length > 1)
            {
                ReportHub.LogWarning(ReportCategory.DEBUG, "[MCP Bootstrap] Another instance already exists, destroying this one");
                Destroy(gameObject);
                return;
            }

            DontDestroyOnLoad(gameObject);

            if (startOnAwake)
                StartServerAsync().Forget();
        }

        /// <summary>
        ///     Запускает MCP WebSocket сервер
        /// </summary>
        public async UniTaskVoid StartServerAsync()
        {
            if (server != null)
            {
                ReportHub.LogWarning(ReportCategory.DEBUG, "[MCP Bootstrap] Server already running");
                return;
            }

            try
            {
                server = new MCPWebSocketServer(port);

                // Можно зарегистрировать дополнительные кастомные команды здесь
                // server.RegisterHandler("customCommand", HandleCustomCommand);

                server.Start();

                ReportHub.Log(ReportCategory.DEBUG, $"[MCP Bootstrap] MCP WebSocket Server successfully started on port {port}");
            }
            catch (Exception e) { ReportHub.LogError(ReportCategory.DEBUG, $"[MCP Bootstrap] Failed to start MCP Server: {e}"); }
        }

        /// <summary>
        ///     Останавливает MCP WebSocket сервер
        /// </summary>
        public void StopServer()
        {
            if (server == null)
            {
                ReportHub.LogWarning(ReportCategory.DEBUG, "[MCP Bootstrap] Server is not running");
                return;
            }

            server.Dispose();
            server = null;

            ReportHub.Log(ReportCategory.DEBUG, "[MCP Bootstrap] MCP WebSocket Server stopped");
        }

        private void OnDestroy() =>
            StopServer();

        private void OnApplicationQuit() =>
            StopServer();

        // Пример кастомного обработчика команды
        // private async UniTask<object> HandleCustomCommand(JObject parameters)
        // {
        //     string param = parameters["someParam"]?.ToString();
        //
        //     return new
        //     {
        //         success = true,
        //         message = $"Custom command executed with param: {param}"
        //     };
        // }
    }
}
