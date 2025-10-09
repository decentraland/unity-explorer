using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System;

namespace DCL.MCP.Handlers
{
    /// <summary>
    ///     Обработчик MCP команды создания GLTFContainer: кладёт запрос в очередь и возвращает requestId.
    ///     Создание выполняется системой MCPSceneCreationSystem в тике сцены.
    /// </summary>
    public class MCPGltfContainerHandler
    {
        public async UniTask<object> HandleCreateGltfContainerAsync(JObject parameters)
        {
            var requestId = Guid.NewGuid().ToString("N");

            string src = parameters["src"]?.ToString() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(src))
                return new { success = false, requestId, error = "Parameter 'src' is required" };

            var req = new Systems.MCPSceneEntitiesBuilder.MCPCreateGltfContainerRequest
            {
                RequestId = requestId,

                // Transform
                X = parameters["x"]?.Value<float?>() ?? 8f,
                Y = parameters["y"]?.Value<float?>() ?? 1f,
                Z = parameters["z"]?.Value<float?>() ?? 8f,
                SX = parameters["sx"]?.Value<float?>() ?? 1f,
                SY = parameters["sy"]?.Value<float?>() ?? 1f,
                SZ = parameters["sz"]?.Value<float?>() ?? 1f,
                Yaw = parameters["yaw"]?.Value<float?>() ?? 0f,
                Pitch = parameters["pitch"]?.Value<float?>() ?? 0f,
                Roll = parameters["roll"]?.Value<float?>() ?? 0f,
                ParentId = parameters["parentId"]?.Value<int?>() ?? 0,

                // GLTF
                Src = src,
            };

            Systems.MCPSceneEntitiesBuilder.EnqueueGltfContainer(req);

            return new
            {
                success = true,
                requestId,
                note = "GltfContainer creation scheduled; will appear shortly",
            };
        }
    }
}
