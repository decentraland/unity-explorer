using Arch.Core;
using CRDT;
using Cysharp.Threading.Tasks;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using ECS.SceneLifeCycle;
using Newtonsoft.Json.Linq;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;

namespace DCL.McpServer.Tools
{
    /// <summary>
    ///     Lists the entities of the current scene's ECS world with their CRDT id and a short description, reading the
    ///     world without its sync lock like the existing debug tooling, so the result is a diagnostic snapshot.
    /// </summary>
    public class ListSceneEntitiesTool : McpTool
    {
        private const int DEFAULT_LIMIT = 200;
        private const int MAX_LIMIT = 2000;

        private static readonly QueryDescription ALL_ENTITIES = new QueryDescription().WithNone<ListMarker>();

        private readonly IScenesCache scenesCache;

        public override string Name => "list_scene_entities";

        public override string Description =>
            "List the entities of the scene at the player's current parcel. Each entry carries the engine id (what get_entity_details takes), "
            + "the CRDT id the scene code uses, and a label describing what the entity shows (gltf src, primitive mesh, text, hidden, parent CRDT id; "
            + "scene root / player / camera for the reserved entities). The scene container (the scene's Unity root at its base parcel) is listed "
            + "without a CRDT id. Pass crdtEntity to find the engine id of an entity the scene logs, or filter "
            + "to search the labels (e.g. 'PoolToy', 'mesh Sphere', 'hidden') instead of calling get_entity_details on every id.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.Integer("limit", "Maximum entities to return. Default 200.")
                  .String("filter", "Case-insensitive text matched against each entity's label; entities without a label never match.")
                  .Integer("crdtEntity", "Return only the entity with this CRDT id.");

        public override JObject OutputSchema =>
            McpJsonSchema.Object()
                          .Integer("total", "Entities in the scene world.")
                          .Integer("matched", "Entities left after filter / crdtEntity.")
                          .Integer("returned")
                          .Boolean("truncated")
                          .IntegerArray("entityIds", "Engine ids of the returned entities, in the same order as entities.")
                          .ObjectArray("entities", McpJsonSchema.Object()
                                                                 .Integer("id", "Engine entity id, for get_entity_details.")
                                                                 .Integer("crdtEntity", "CRDT id the scene code uses; null for engine-only entities.", nullable: true)
                                                                 .String("label", "What the entity shows; null when nothing describes it.", nullable: true),
                              "Returned entities sorted by engine id.")
                          .Build();

        public override McpToolAnnotations Annotations => McpToolAnnotations.ReadOnly();

        public ListSceneEntitiesTool(IScenesCache scenesCache)
        {
            this.scenesCache = scenesCache;
        }

        public override UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            int limit = Mathf.Clamp(arguments.GetInt("limit", DEFAULT_LIMIT), 1, MAX_LIMIT);
            string? filter = arguments.GetStringOrNull("filter");
            bool byCrdtEntity = arguments.TryGetInt("crdtEntity", out int crdtEntityFilter);

            ISceneFacade? scene = scenesCache.CurrentScene.Value;

            if (scene == null)
                return UniTask.FromResult(McpToolResult.Error("No scene world found at the current parcel."));

            World world = scene.EcsExecutor.World;
            var entities = new List<Entity>();
            world.Query(in ALL_ENTITIES, entity => entities.Add(entity));
            entities.Sort(static (a, b) => a.Id.CompareTo(b.Id));

            var matches = new List<EntityEntry>();
            var parts = new List<string>(5);

            foreach (Entity entity in entities)
            {
                int? crdtEntity = world.TryGet(entity, out CRDTEntity crdt) ? crdt.Id : null;
                bool sceneContainer = crdtEntity.HasValue && SceneEntityLabel.IsSceneContainer(world, entity, crdtEntity.Value);

                if (sceneContainer)
                    crdtEntity = null;

                if (byCrdtEntity && crdtEntity != crdtEntityFilter) continue;

                string? label = sceneContainer ? "scene container"
                    : crdtEntity.HasValue ? SceneEntityLabel.Reserved(crdtEntity.Value) ?? SceneEntityLabel.Describe(world, entity, parts)
                    : null;

                if (!string.IsNullOrEmpty(filter) && (label == null || label.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)) continue;

                matches.Add(new EntityEntry(entity.Id, crdtEntity, label));
            }

            int returned = Mathf.Min(limit, matches.Count);
            bool truncated = returned < matches.Count;
            bool filtered = byCrdtEntity || !string.IsNullOrEmpty(filter);

            var ids = new JArray();
            var entries = new JArray();
            var output = new StringBuilder();
            output.Append("total=").Append(entities.Count);

            if (filtered)
                output.Append(" matched=").Append(matches.Count);

            output.Append(" returned=").Append(returned).AppendLine();

            for (var i = 0; i < returned; i++)
            {
                EntityEntry entry = matches[i];
                ids.Add(entry.Id);

                entries.Add(new JObject
                {
                    ["id"] = entry.Id,
                    ["crdtEntity"] = entry.CrdtEntity.HasValue ? new JValue(entry.CrdtEntity.Value) : JValue.CreateNull(),
                    ["label"] = entry.Label != null ? new JValue(entry.Label) : JValue.CreateNull(),
                });

                output.Append(entry.Id);

                if (entry.CrdtEntity.HasValue)
                    output.Append(" crdt=").Append(entry.CrdtEntity.Value);

                if (entry.Label != null)
                    output.Append(" (").Append(entry.Label).Append(')');

                output.AppendLine();
            }

            if (truncated)
            {
                output.Append(returned)
                      .Append(" of ")
                      .Append(matches.Count)
                      .Append(" shown; raise limit (max ")
                      .Append(MAX_LIMIT)
                      .Append(") or narrow with filter to see the rest.");
            }

            var structured = new JObject
            {
                ["total"] = entities.Count,
                ["matched"] = matches.Count,
                ["returned"] = returned,
                ["truncated"] = truncated,
                ["entityIds"] = ids,
                ["entities"] = entries,
            };

            return UniTask.FromResult(McpToolResult.TextWithStructured(output.ToString(), structured));
        }

        private readonly struct EntityEntry
        {
            public readonly int Id;
            public readonly int? CrdtEntity;
            public readonly string? Label;

            public EntityEntry(int id, int? crdtEntity, string? label)
            {
                Id = id;
                CrdtEntity = crdtEntity;
                Label = label;
            }
        }

        private struct ListMarker { }
    }
}
