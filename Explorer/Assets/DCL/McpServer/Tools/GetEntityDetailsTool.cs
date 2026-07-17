using Cysharp.Threading.Tasks;
using DCL.McpServer.Core;
using Newtonsoft.Json.Linq;
using SceneRunner.Debugging;
using SceneRunner.Debugging.Hub;
using System.Text;
using System.Threading;

namespace DCL.McpServer.Tools
{
    public class GetEntityDetailsTool : IMcpTool
    {
        private const int MAX_CHARS = 8000;
        private const string CURRENT_SCENE = "CURRENT";

        private readonly IWorldInfoHub worldInfoHub;

        public string Name => "get_entity_details";

        public string Description =>
            "Dump all components of one entity in the current scene's ECS world (ids come from list_scene_entities).";

        public JObject InputSchema =>
            McpInputSchema.Object()
                          .Integer("entityId", "Entity id within the current scene world.", required: true)
                          .Build();

        public McpToolAnnotations Annotations => McpToolAnnotations.ReadOnly();

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

            string dump = worldInfo.EntityComponentsInfo(entityId);

            if (dump.Length <= MAX_CHARS)
                return McpToolResult.Text(dump);

            var output = new StringBuilder(MAX_CHARS + 64);
            output.Append(dump, 0, MAX_CHARS);
            output.AppendLine();
            output.Append($"... output truncated at {MAX_CHARS}/{dump.Length} chars");

            return McpToolResult.Text(output.ToString());
        }
    }
}
