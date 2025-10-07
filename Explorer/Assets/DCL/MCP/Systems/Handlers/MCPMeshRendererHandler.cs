using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System;

namespace DCL.MCP.Handlers
{
    /// <summary>
    ///     Обработчик MCP команды создания MeshRenderer: кладёт запрос в очередь и возвращает requestId.
    ///     Создание выполняется системой MCPSceneCreationSystem в тике сцены.
    /// </summary>
    public class MCPMeshRendererHandler
    {
        public async UniTask<object> HandleCreateMeshRendererAsync(JObject parameters)
        {
            var requestId = Guid.NewGuid().ToString("N");

            var req = new Systems.MCPSceneEntitiesBuilder.MCPCreateMeshRendererRequest
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

                // Mesh
                MeshType = parameters["meshType"]?.ToString() ?? "Box",
                RadiusTop = parameters["radiusTop"]?.Value<float?>(),
                RadiusBottom = parameters["radiusBottom"]?.Value<float?>(),
                Uvs = parameters["uvs"]?.ToObject<System.Collections.Generic.List<float>>() ?? null,
            };

            Systems.MCPSceneEntitiesBuilder.EnqueueMeshRenderer(req);

            return new
            {
                success = true,
                requestId,
                note = "MeshRenderer creation scheduled; will appear shortly",
            };
        }
    }
}
