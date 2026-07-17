using Cysharp.Threading.Tasks;
using DCL.McpServer.Core;
using Newtonsoft.Json.Linq;
using SceneRunner.Debugging;
using SceneRunner.Debugging.Hub;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;

namespace DCL.McpServer.Tools
{
    /// <summary>
    ///     Lists the entity ids of the current scene's ECS world through the same unsynchronized
    ///     <see cref="IWorldInfoHub" /> path the existing debug tooling uses.
    /// </summary>
    public class ListSceneEntitiesTool : IMcpTool
    {
        private const int DEFAULT_LIMIT = 200;
        private const int MAX_LIMIT = 2000;
        private const string CURRENT_SCENE = "CURRENT";

        private readonly IWorldInfoHub worldInfoHub;

        public string Name => "list_scene_entities";

        public string Description =>
            "List the ECS entity ids of the scene at the player's current parcel. Feed an id into get_entity_details to inspect its components.";

        public string InputSchemaJson =>
            @"{
                ""type"": ""object"",
                ""properties"": {
                    ""limit"": { ""type"": ""integer"", ""description"": ""Maximum ids to return. Default 200."" }
                }
            }";

        public ListSceneEntitiesTool(IWorldInfoHub worldInfoHub)
        {
            this.worldInfoHub = worldInfoHub;
        }

        public async UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            int limit = Mathf.Clamp(arguments.GetInt("limit", DEFAULT_LIMIT), 1, MAX_LIMIT);

            await UniTask.SwitchToMainThread(ct);

            IWorldInfo? worldInfo = worldInfoHub.WorldInfo(CURRENT_SCENE);

            if (worldInfo == null)
                return McpToolResult.Error("No scene world found at the current parcel.");

            IReadOnlyList<int> entityIds = worldInfo.EntityIds();

            var output = new StringBuilder();
            output.AppendLine($"total={entityIds.Count} returned={Mathf.Min(limit, entityIds.Count)}");

            for (var i = 0; i < entityIds.Count && i < limit; i++)
            {
                output.Append(entityIds[i]);
                output.Append(i < entityIds.Count - 1 && i < limit - 1 ? ", " : string.Empty);
            }

            return McpToolResult.Text(output.ToString());
        }
    }
}
