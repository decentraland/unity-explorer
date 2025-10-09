using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System;

namespace DCL.MCP.Handlers
{
    /// <summary>
    ///     Обработчик MCP команды изменения позиции существующей entity.
    ///     Не создаёт entity, только отправляет PutMessage через очередь билдера.
    /// </summary>
    public class MCPTransformHandler
    {
        public async UniTask<object> HandleSetEntityPositionAsync(JObject parameters)
        {
            var requestId = Guid.NewGuid().ToString("N");

            int entityId = parameters["entityId"]?.Value<int?>() ?? -1;

            if (entityId <= 0)
            {
                return new
                {
                    success = false,
                    error = "Invalid or missing entityId",
                };
            }

            var req = new Systems.MCPSceneEntitiesBuilder.MCPSetEntityTransformRequest
            {
                RequestId = requestId,
                EntityId = entityId,
                X = parameters["x"]?.Value<float?>(),
                Y = parameters["y"]?.Value<float?>(),
                Z = parameters["z"]?.Value<float?>(),
                SX = parameters["sx"]?.Value<float?>(),
                SY = parameters["sy"]?.Value<float?>(),
                SZ = parameters["sz"]?.Value<float?>(),
                Yaw = parameters["yaw"]?.Value<float?>(),
                Pitch = parameters["pitch"]?.Value<float?>(),
                Roll = parameters["roll"]?.Value<float?>(),
            };

            Systems.MCPSceneEntitiesBuilder.EnqueueSetEntityTransform(req);

            return new
            {
                success = true,
                requestId,
                note = "SetEntityTransform scheduled",
            };
        }
    }
}
