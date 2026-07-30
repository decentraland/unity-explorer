using CRDT.Attribution;
using Cysharp.Threading.Tasks;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using ECS.SceneLifeCycle;
using Newtonsoft.Json.Linq;
using SceneRunner.Debugging;
using SceneRunner.Debugging.Hub;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace DCL.McpServer.Tools
{
    public class GetEntityDetailsTool : McpTool
    {
        private const int MAX_CHARS = 8000;
        private const string CURRENT_SCENE = "CURRENT";

        private readonly IWorldInfoHub worldInfoHub;
        private readonly IScenesCache scenesCache;
        private readonly ICrdtWriterLog writerLog;

        private readonly List<CrdtWrite> writesBuffer = new ();

        public override string Name => "get_entity_details";

        public override string Description =>
            "Dump all components of one entity in the current scene's ECS world (ids come from list_scene_entities), "
            + "and report which address last wrote each of its networked components. A component absent from that report was "
            + "written by the scene's own code and never asserted by a peer; a row flagged viaStateSync was relayed in a peer's "
            + "state dump and does not identify an author.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.Integer("entityId", "Entity id within the current scene world.", isRequired: true);

        public override JObject OutputSchema =>
            McpJsonSchema.Object()
                          .Integer("entityId", "The entity that was asked for, as an index into the scene's ECS world.")
                          .Integer("crdtEntityId", "The same entity as the scene's own code and the scene room address it, or null when it is not backed by a CRDT entity.", nullable: true)
                          .ObjectArray("networkWrites", CrdtAttributionJson.WriteSchema(),
                               "Last observed write per component of this entity that arrived over the scene room, most recent first. "
                               + "Empty when nothing about this entity came from a peer. The component dump itself is in the text content.")
                          .Build();

        public override McpToolAnnotations Annotations => McpToolAnnotations.ReadOnly();

        public GetEntityDetailsTool(IWorldInfoHub worldInfoHub, IScenesCache scenesCache, ICrdtWriterLog writerLog)
        {
            this.worldInfoHub = worldInfoHub;
            this.scenesCache = scenesCache;
            this.writerLog = writerLog;
        }

        public override UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            if (!arguments.TryGetInt("entityId", out int entityId))
                return UniTask.FromResult(McpToolResult.Error("entityId is required."));

            IWorldInfo? worldInfo = worldInfoHub.WorldInfo(CURRENT_SCENE);

            if (worldInfo == null)
                return UniTask.FromResult(McpToolResult.Error("No scene world found at the current parcel."));

            string dump = worldInfo.EntityComponentsInfo(entityId);
            int? crdtEntityId = worldInfo.CrdtEntityId(entityId);

            CollectWrites(crdtEntityId);

            var output = new StringBuilder(MAX_CHARS + 512);

            if (dump.Length <= MAX_CHARS)
                output.Append(dump);
            else
                output.Append(dump, 0, MAX_CHARS)
                      .AppendLine()
                      .Append("... output truncated at ")
                      .Append(MAX_CHARS)
                      .Append('/')
                      .Append(dump.Length)
                      .Append(" chars");

            CrdtAttributionJson.AppendWrites(output, writesBuffer);

            var structured = new JObject
            {
                ["entityId"] = entityId,
                ["crdtEntityId"] = crdtEntityId.HasValue ? new JValue(crdtEntityId.Value) : JValue.CreateNull(),
                ["networkWrites"] = CrdtAttributionJson.Writes(writesBuffer),
            };

            return UniTask.FromResult(McpToolResult.TextWithStructured(output.ToString(), structured));
        }

        /// <summary>
        ///     Attribution is keyed by CRDT entity, so an entity the scene never registered as one — and every entity
        ///     while no scene is current — has no rows rather than borrowing another entity's.
        /// </summary>
        private void CollectWrites(int? crdtEntityId)
        {
            writesBuffer.Clear();

            string? sceneId = scenesCache.CurrentScene.Value?.SceneData.SceneEntityDefinition.id;

            if (sceneId == null || !crdtEntityId.HasValue)
                return;

            writerLog.EntityWrites(sceneId, crdtEntityId.Value, writesBuffer);
            writesBuffer.Sort(static (left, right) => left.AgeSeconds.CompareTo(right.AgeSeconds));
        }
    }
}
