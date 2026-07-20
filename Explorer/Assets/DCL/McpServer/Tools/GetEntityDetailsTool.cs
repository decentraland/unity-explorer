using Cysharp.Threading.Tasks;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using Newtonsoft.Json.Linq;
using SceneRunner.Debugging;
using SceneRunner.Debugging.Hub;
using System.Text;
using System.Threading;

namespace DCL.McpServer.Tools
{
    public class GetEntityDetailsTool : McpTool
    {
        private const int MAX_CHARS = 8000;
        private const string CURRENT_SCENE = "CURRENT";

        private readonly IWorldInfoHub worldInfoHub;

        public override string Name => "get_entity_details";

        public override string Description =>
            "Dump all components of one entity in the current scene's ECS world (ids come from list_scene_entities).";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.Integer("entityId", "Entity id within the current scene world.", isRequired: true);

        public override McpToolAnnotations Annotations => McpToolAnnotations.ReadOnly();

        public GetEntityDetailsTool(IWorldInfoHub worldInfoHub)
        {
            this.worldInfoHub = worldInfoHub;
        }

        protected override UniTask<McpToolResult> ExecuteCoreAsync(JObject arguments, CancellationToken ct)
        {
            if (!arguments.TryGetInt("entityId", out int entityId))
                return UniTask.FromResult(McpToolResult.Error("entityId is required."));

            IWorldInfo? worldInfo = worldInfoHub.WorldInfo(CURRENT_SCENE);

            if (worldInfo == null)
                return UniTask.FromResult(McpToolResult.Error("No scene world found at the current parcel."));

            string dump = worldInfo.EntityComponentsInfo(entityId);

            if (dump.Length <= MAX_CHARS)
                return UniTask.FromResult(McpToolResult.Text(dump));

            var output = new StringBuilder(MAX_CHARS + 64);
            output.Append(dump, 0, MAX_CHARS);
            output.AppendLine();
            output.Append("... output truncated at ")
                  .Append(MAX_CHARS)
                  .Append('/')
                  .Append(dump.Length)
                  .Append(" chars");

            return UniTask.FromResult(McpToolResult.Text(output.ToString()));
        }
    }
}
