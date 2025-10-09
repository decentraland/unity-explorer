using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System;

namespace DCL.MCP.Handlers
{
    /// <summary>
    ///     Обработчик MCP команды создания TriggerArea: кладёт запрос в очередь и возвращает requestId.
    ///     One-time реакция (цилиндр) реализуется в билдере при обработке запроса.
    /// </summary>
    public class MCPTriggerAreaHandler
    {
        public async UniTask<object> HandleCreateTriggerAreaAsync(JObject parameters)
        {
            var requestId = Guid.NewGuid().ToString("N");

            var req = new Systems.MCPSceneEntitiesBuilder.MCPCreateTriggerAreaRequest
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

                // TriggerArea
                MeshType = parameters["meshType"]?.ToString() ?? "Box",
            };

            Systems.MCPSceneEntitiesBuilder.EnqueueTriggerArea(req);

            return new
            {
                success = true,
                requestId,
                note = "TriggerArea creation scheduled; one-time Cylinder will be spawned automatically",
            };
        }
    }
}
