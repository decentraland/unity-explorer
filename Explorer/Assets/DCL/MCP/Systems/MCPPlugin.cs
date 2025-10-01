using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using System;
using System.Threading;

namespace DCL.MCP
{
    /// <summary>
    ///     Глобальный плагин для автоматической инициализации MCP WebSocket сервера.
    ///     Запускается при старте приложения и живёт на протяжении всей сессии.
    /// </summary>
    public class MCPPlugin : IDCLGlobalPluginWithoutSettings
    {
        private const int DEFAULT_PORT = 7777;

        private MCPWebSocketServer server;

        public async UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct)
        {
            try
            {
                server = new MCPWebSocketServer();

                // Можно зарегистрировать дополнительные кастомные команды здесь
                // server.RegisterHandler("customCommand", HandleCustomCommand);

                server.Start();

                ReportHub.Log(ReportCategory.DEBUG, $"[MCP Plugin] MCP WebSocket Server successfully started on port {DEFAULT_PORT}");
            }
            catch (Exception e)
            {
                ReportHub.LogError(ReportCategory.DEBUG, $"[MCP Plugin] Failed to start MCP Server: {e}");
                throw;
            }
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            // MCP Server не требует инъекции систем в ECS World
        }

        public void Dispose()
        {
            server?.Dispose();
            server = null;

            ReportHub.Log(ReportCategory.DEBUG, "[MCP Plugin] MCP Plugin disposed");
        }

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
