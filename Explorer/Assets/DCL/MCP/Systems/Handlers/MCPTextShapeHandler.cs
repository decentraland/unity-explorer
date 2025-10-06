using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System;

namespace DCL.MCP.Handlers
{
    /// <summary>
    ///     Обработчик MCP команды создания TextShape: кладёт запрос в очередь и возвращает requestId.
    ///     Создание выполняется системой MCPSceneCreationSystem в тике сцены.
    /// </summary>
    public class MCPTextShapeHandler
    {
        public async UniTask<object> HandleCreateTextShapeAsync(JObject parameters)
        {
            var requestId = Guid.NewGuid().ToString("N");

            var req = new Systems.MCPSceneEntitiesBuilder.MCPCreateTextShapeRequest
            {
                RequestId = requestId,

                // Transform
                X = parameters["x"]?.Value<float?>() ?? 8f,
                Y = parameters["y"]?.Value<float?>() ?? 4f,
                Z = parameters["z"]?.Value<float?>() ?? 8f,
                SX = parameters["sx"]?.Value<float?>() ?? 1f,
                SY = parameters["sy"]?.Value<float?>() ?? 1f,
                SZ = parameters["sz"]?.Value<float?>() ?? 1f,
                Yaw = parameters["rotationEuler"]?["yaw"]?.Value<float?>() ?? parameters["yaw"]?.Value<float?>() ?? 0f,
                Pitch = parameters["rotationEuler"]?["pitch"]?.Value<float?>() ?? parameters["pitch"]?.Value<float?>() ?? 0f,
                Roll = parameters["rotationEuler"]?["roll"]?.Value<float?>() ?? parameters["roll"]?.Value<float?>() ?? 0f,
                ParentId = parameters["parentId"]?.Value<int?>() ?? 0,

                // Text content & style (имена соответствуют Builder.TextShape.cs)
                Text = parameters["text"]?.ToString() ?? string.Empty,
                FontSize = parameters["fontSize"]?.Value<float?>() ?? 5f,
                Font = parameters["font"]?.ToString() ?? "FSansSerif",
                FontAutoSize = parameters["fontAutoSize"]?.Value<bool?>() ?? false,
                TextAlign = parameters["textAlign"]?.ToString() ?? "TamTopLeft",
                Width = parameters["width"]?.Value<float?>() ?? 4f,
                Height = parameters["height"]?.Value<float?>() ?? 2f,
                PaddingTop = parameters["paddingTop"]?.Value<float?>() ?? 0f,
                PaddingRight = parameters["paddingRight"]?.Value<float?>() ?? 0f,
                PaddingBottom = parameters["paddingBottom"]?.Value<float?>() ?? 0f,
                PaddingLeft = parameters["paddingLeft"]?.Value<float?>() ?? 0f,
                LineSpacing = parameters["lineSpacing"]?.Value<float?>() ?? 0f,
                LineCount = parameters["lineCount"]?.Value<int?>() ?? 0,
                TextWrapping = parameters["textWrapping"]?.Value<bool?>() ?? false,
                ShadowBlur = parameters["shadowBlur"]?.Value<float?>() ?? 0f,
                ShadowOffsetX = parameters["shadowOffsetX"]?.Value<float?>() ?? 0f,
                ShadowOffsetY = parameters["shadowOffsetY"]?.Value<float?>() ?? 0f,
                OutlineWidth = parameters["outlineWidth"]?.Value<float?>() ?? 0.1f,
                ShadowColor = parameters["shadowColor"]?.ToObject<Decentraland.Common.Color3?>(),
                OutlineColor = parameters["outlineColor"]?.ToObject<Decentraland.Common.Color3?>(),
                TextColor = parameters["textColor"]?.ToObject<Decentraland.Common.Color4?>(),
            };

            Systems.MCPSceneEntitiesBuilder.EnqueueTextShape(req);

            return new
            {
                success = true,
                requestId,
                note = "TextShape creation scheduled; result will be sent via 'textShapeCreated' event",
            };
        }
    }
}
