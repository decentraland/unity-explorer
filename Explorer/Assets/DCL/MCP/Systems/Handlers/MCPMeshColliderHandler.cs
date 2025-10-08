using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System;

namespace DCL.MCP.Handlers
{
    /// <summary>
    ///     Обработчик MCP команды создания MeshCollider: кладёт запрос в очередь и возвращает requestId.
    ///     Создание выполняется системой MCPSceneCreationSystem в тике сцены.
    /// </summary>
    public class MCPMeshColliderHandler
    {
        public async UniTask<object> HandleCreateMeshColliderAsync(JObject parameters)
        {
            var requestId = Guid.NewGuid().ToString("N");

            var req = new Systems.MCPSceneEntitiesBuilder.MCPCreateMeshColliderRequest
            {
                RequestId = requestId,
                ColliderType = parameters["colliderType"]?.ToString() ?? "Box",
                CollisionMask = parameters["collisionMask"]?.Value<uint?>() ?? 1u | 2u, // CL_POINTER | CL_PHYSICS
                RadiusTop = parameters["radiusTop"]?.Value<float?>(),
                RadiusBottom = parameters["radiusBottom"]?.Value<float?>(),
            };

            Systems.MCPSceneEntitiesBuilder.EnqueueMeshCollider(req);

            return new
            {
                success = true,
                requestId,
                note = "MeshCollider creation scheduled; will appear shortly",
            };
        }
    }
}
