using Cysharp.Threading.Tasks;
using DCL.Mcp.Server;
using Newtonsoft.Json.Linq;
using SceneRunner.Debugging;
using SceneRunner.Debugging.Hub;
using System.Threading;

namespace DCL.Mcp.Tools
{
    public class GetEntityDetailsTool : IMcpTool
    {
        private const string CURRENT_SCENE = "CURRENT";

        private readonly IWorldInfoHub worldInfoHub;

        public string Name => "get_entity_details";

        public string Description =>
            "Dump all components of one entity in the current scene's ECS world (ids come from list_scene_entities).";

        public string InputSchemaJson =>
            @"{
                ""type"": ""object"",
                ""properties"": {
                    ""entityId"": { ""type"": ""integer"", ""description"": ""Entity id within the current scene world."" }
                },
                ""required"": [""entityId""]
            }";

        public GetEntityDetailsTool(IWorldInfoHub worldInfoHub)
        {
            this.worldInfoHub = worldInfoHub;
        }

        public async UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            if (!arguments.TryGetInt("entityId", out int entityId))
                return McpToolResult.Error("entityId is required.");

            await UniTask.SwitchToMainThread(ct);

            IWorldInfo? worldInfo = worldInfoHub.WorldInfo(CURRENT_SCENE);

            if (worldInfo == null)
                return McpToolResult.Error("No scene world found at the current parcel.");

            return McpToolResult.Text(worldInfo.EntityComponentsInfo(entityId));
        }
    }
}
