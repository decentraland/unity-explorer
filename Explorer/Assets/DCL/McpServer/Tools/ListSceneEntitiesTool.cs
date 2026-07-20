using Cysharp.Threading.Tasks;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
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

        public JObject InputSchema =>
            McpJsonSchema.Object()
                          .Integer("limit", "Maximum ids to return. Default 200.")
                          .Build();

        public JObject OutputSchema =>
            McpJsonSchema.Object()
                          .Integer("total")
                          .Integer("returned")
                          .Boolean("truncated")
                          .IntegerArray("entityIds")
                          .Build();

        public McpToolAnnotations Annotations => McpToolAnnotations.ReadOnly();

        public ListSceneEntitiesTool(IWorldInfoHub worldInfoHub)
        {
            this.worldInfoHub = worldInfoHub;
        }

        public UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            int limit = Mathf.Clamp(arguments.GetInt("limit", DEFAULT_LIMIT), 1, MAX_LIMIT);

            IWorldInfo? worldInfo = worldInfoHub.WorldInfo(CURRENT_SCENE);

            if (worldInfo == null)
                return UniTask.FromResult(McpToolResult.Error("No scene world found at the current parcel."));

            IReadOnlyList<int> entityIds = worldInfo.EntityIds();
            int returned = Mathf.Min(limit, entityIds.Count);
            bool truncated = returned < entityIds.Count;

            var ids = new JArray();
            var output = new StringBuilder();
            output.AppendLine($"total={entityIds.Count} returned={returned}");

            for (var i = 0; i < entityIds.Count && i < limit; i++)
            {
                ids.Add(entityIds[i]);
                output.Append(entityIds[i]);
                output.Append(i < entityIds.Count - 1 && i < limit - 1 ? ", " : string.Empty);
            }

            if (truncated)
            {
                output.AppendLine();
                output.Append($"{returned} of {entityIds.Count} shown; raise limit (max {MAX_LIMIT}) to see the rest.");
            }

            var structured = new JObject
            {
                ["total"] = entityIds.Count,
                ["returned"] = returned,
                ["truncated"] = truncated,
                ["entityIds"] = ids,
            };

            return UniTask.FromResult(McpToolResult.TextWithStructured(output.ToString(), structured));
        }
    }
}
