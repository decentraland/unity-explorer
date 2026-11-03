using Arch.Core;
using CRDT;
using Cysharp.Threading.Tasks;
using DCL.ECS7;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using DCL.Profiling;
using ECS.SceneLifeCycle;
using Newtonsoft.Json.Linq;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Threading;
using UnityEngine;

namespace DCL.McpServer.Tools
{
    /// <summary>
    ///     Captures every CRDT message exchanged between the current scene and the renderer over a short
    ///     window and reports which of them changed nothing, grouped by entity and component, so an agent
    ///     holding the scene source can find the system that writes a component every tick without a change.
    /// </summary>
    public class GetCrdtTrafficTool : McpTool
    {
        private const float DEFAULT_SAMPLE_SECONDS = 2f;
        private const float MIN_SAMPLE_SECONDS = 0.5f;
        private const float MAX_SAMPLE_SECONDS = 10f;
        private const int DEFAULT_LIMIT = 15;
        private const int MAX_LIMIT = 100;

        /// <summary>
        ///     Scene and renderer ticks drift by a frame or two over a window, so a writer that fires on every tick
        ///     can be a few messages short of the tick count.
        /// </summary>
        private const float EVERY_TICK_RATIO = 0.95f;

        private static readonly QueryDescription CRDT_ENTITIES = new QueryDescription().WithAll<CRDTEntity>();

        private static readonly Dictionary<int, string> COMPONENT_NAMES = BuildComponentNames();

        private static readonly Comparison<CrdtTrafficProbe.EntrySnapshot> BY_MESSAGES_DESC = static (a, b) =>
            b.Messages != a.Messages ? b.Messages.CompareTo(a.Messages) : b.Bytes.CompareTo(a.Bytes);

        private readonly IScenesCache scenesCache;
        private readonly WaitForWindow waitForWindow;

        public override string Name => "get_crdt_traffic";

        public override string Description =>
            "Capture every CRDT message exchanged between the current scene's JS runtime and the renderer over a short window, in both directions, "
            + "and classify each one: applied (changed the renderer state) or wasted (rewrote identical values with a newer timestamp, duplicated the "
            + "current state, arrived stale, targeted a deleted entity, or was a Creator Hub component leaked from main.composite). Reports totals, "
            + "the per-tick rate and the top writers grouped by entity + component with their cadence and a verdict. A writer flagged 'rewrites identical "
            + "values every tick' is scene code re-setting a component to the same value every frame — createOrReplace or a helper built on it "
            + "(MeshRenderer.setBox/setPlane, Material.set*), which the SDK always sends; grep the scene source for that component to find it. "
            + "An unchanged getMutable is not sent since SDK 7.22.3, except on SDK builds that resend after an incoming update of the same component. Each writer carries engineEntityId (the id get_entity_details takes; "
            + "'entity' is the CRDT id the scene uses) and an entityLabel describing it (GLTF src, primitive mesh, text, hidden, parent). "
            + "Scene-defined custom components never appear: the SDK does not send them to the renderer. bridgeTime reports the wall time the CRDT "
            + "bridge spent per scene tick (incoming reconcile, apply under the scene world lock, outgoing serialize). It is elapsed time on the "
            + "scene thread, so it includes the thread being preempted and varies between captures: compare several captures, and for a fix to "
            + "scene writes compare incomingUsPerTick, the stage that scales with the scene's messages. It measures what render FPS is too noisy "
            + "to show, and excludes the ECS systems that later react to the dirtied components. The call holds for sampleSeconds while it captures. Pair with get_performance_stats: "
            + "msgs/tick from the scene is the cost the Current Scene debug panel charts.";

        public override JObject OutputSchema =>
            McpJsonSchema.Object()
                          .Number("sampleSeconds")
                          .Integer("sceneTicks", "Scene ticks (JS frames) observed during the window; per-tick rates divide by this.")
                          .Object("fromScene", DirectionSchema(), "Messages the scene sent to the renderer.")
                          .Object("toScene", DirectionSchema(), "Messages the renderer sent to the scene (player/camera transforms, pointer events, engine info, load states).")
                          .Object("bridgeTime", McpJsonSchema.Object()
                                                             .Number("totalMs", "CRDT bridge wall time over the whole window.")
                                                             .Number("totalUsPerTick", "Average microseconds per scene tick: the sum of the three stages.")
                                                             .Number("incomingUsPerTick", "Deserializing the scene's messages, reconciling them and preparing the world sync buffer (scene thread).")
                                                             .Number("applyUsPerTick", "Applying the sync buffer to the ECS world while holding the scene world lock, which blocks the main thread from that world.")
                                                             .Number("outgoingUsPerTick", "Serializing the renderer's messages back to the scene."),
                              "Wall time the CRDT bridge spent on the exchanges (elapsed, so it includes preemption of the scene thread), excluding the capture's own classification work and the ECS systems reacting afterwards.")
                          .Build();

        public override McpToolAnnotations Annotations => McpToolAnnotations.ReadOnly();

        public GetCrdtTrafficTool(IScenesCache scenesCache, WaitForWindow? waitForWindow = null)
        {
            this.scenesCache = scenesCache;
            this.waitForWindow = waitForWindow ?? WaitRealtimeAsync;
        }

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.Number("sampleSeconds", "Seconds to capture, clamped to 0.5–10. Default 2. The call holds for this duration.")
                  .Integer("limit", "Top writers to list per direction, sorted by message count, clamped to 1–100. Default 15.");

        public override async UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            float sampleSeconds = Mathf.Clamp(arguments.GetFloat("sampleSeconds", DEFAULT_SAMPLE_SECONDS), MIN_SAMPLE_SECONDS, MAX_SAMPLE_SECONDS);
            int limit = Mathf.Clamp(arguments.GetInt("limit", DEFAULT_LIMIT), 1, MAX_LIMIT);

            ISceneFacade? scene = scenesCache.CurrentScene.Value;

            if (scene == null)
                return McpToolResult.Error("No current scene: the player is not standing on a loaded scene.");

            CrdtTrafficProbe probe = scene.RuntimeMetrics.Traffic;

            if (!probe.TryStartCapture())
                return McpToolResult.Error("A CRDT traffic capture is already running for the current scene; wait for it to finish and call again.");

            var entries = new List<CrdtTrafficProbe.EntrySnapshot>();
            CrdtTrafficProbe.Snapshot snapshot;

            try { await waitForWindow(sampleSeconds, ct); }
            finally { snapshot = probe.StopCapture(entries); }

            if (!ReferenceEquals(scenesCache.CurrentScene.Value, scene))
                return McpToolResult.Error("The current scene changed during the capture; call again once the new scene is ready.");

            if (snapshot.Batches == 0)
                return McpToolResult.Error("The scene did not tick during the window (paused, crashed or still loading); check get_scene_state.");

            var fromSceneEntries = new List<CrdtTrafficProbe.EntrySnapshot>();
            var toSceneEntries = new List<CrdtTrafficProbe.EntrySnapshot>();

            foreach (CrdtTrafficProbe.EntrySnapshot entry in entries)
            {
                if (entry.Direction == CrdtTrafficDirection.FromScene)
                    fromSceneEntries.Add(entry);
                else
                    toSceneEntries.Add(entry);
            }

            fromSceneEntries.Sort(BY_MESSAGES_DESC);
            toSceneEntries.Sort(BY_MESSAGES_DESC);

            Dictionary<int, EntityInfo> entities = ResolveEntities(scene.EcsExecutor.World, fromSceneEntries, toSceneEntries, limit);

            var structured = new JObject
            {
                ["sampleSeconds"] = Round1(sampleSeconds),
                ["sceneTicks"] = snapshot.Batches,
                ["fromScene"] = DirectionJson(snapshot.FromScene, snapshot.FromSceneBytes, snapshot.Batches, fromSceneEntries, limit, entities),
                ["toScene"] = DirectionJson(snapshot.ToScene, snapshot.ToSceneBytes, snapshot.Batches, toSceneEntries, limit, entities),
                ["bridgeTime"] = BridgeTimeJson(snapshot.Time, snapshot.Batches),
            };

            var text = new StringBuilder();
            text.Append("CRDT traffic over ").Append(sampleSeconds.ToString("F1", CultureInfo.InvariantCulture)).Append(" s (")
                .Append(snapshot.Batches).AppendLine(" scene ticks).");

            AppendDirectionText(text, "Scene -> renderer", snapshot.FromScene, snapshot.FromSceneBytes, snapshot.Batches, fromSceneEntries, limit, entities);
            AppendDirectionText(text, "Renderer -> scene", snapshot.ToScene, snapshot.ToSceneBytes, snapshot.Batches, toSceneEntries, limit, entities);

            CrdtTrafficProbe.BridgeTime time = snapshot.Time;
            text.Append("CRDT bridge time: ").Append(FormatUs(PerTickUs(time.TotalTicks, snapshot.Batches))).Append(" us/tick (incoming ")
                .Append(FormatUs(PerTickUs(time.IncomingTicks, snapshot.Batches))).Append(", apply under world lock ")
                .Append(FormatUs(PerTickUs(time.ApplyTicks, snapshot.Batches))).Append(", outgoing ")
                .Append(FormatUs(PerTickUs(time.OutgoingTicks, snapshot.Batches))).Append("); ")
                .Append(TicksToMs(time.TotalTicks).ToString("F2", CultureInfo.InvariantCulture)).AppendLine(" ms over the window.");

            return McpToolResult.TextWithStructured(text.ToString(), structured);
        }

        private static McpJsonSchema DirectionSchema() =>
            McpJsonSchema.Object()
                          .Integer("messages")
                          .Integer("bytes", "Payload bytes, excluding CRDT headers.")
                          .Number("perTick")
                          .Integer("applied", "Messages that changed the receiver's state.")
                          .Integer("wasted", "Messages that changed nothing: the sum of the five categories below.")
                          .Integer("redundantIdenticalData", "Newer timestamp, identical payload: the sender re-wrote a component without changing it.")
                          .Integer("noOpSameState", "Same timestamp and payload as the stored state.")
                          .Integer("noOpOutdated", "Discarded by last-write-wins: older timestamp, or same timestamp and lower payload.")
                          .Integer("noOpEntityDeleted", "Targeted an entity that was already deleted.")
                          .Integer("filteredCreatorHub", "Creator Hub components leaked from main.composite, dropped before the protocol.")
                          .Integer("distinctWriters", "Distinct entity + component pairs that sent at least one message.")
                          .ObjectArray("hotWriters", McpJsonSchema.Object()
                                                                   .Integer("entity", "CRDT entity id, as the scene code sees it.")
                                                                   .Integer("engineEntityId", "Engine entity id for get_entity_details; null when the entity no longer exists.", nullable: true)
                                                                   .String("entityLabel", "scene root, player or camera for the SDK reserved entities; otherwise what the entity shows (gltf src, primitive mesh, text, hidden, parent CRDT id).", nullable: true)
                                                                   .Integer("componentId")
                                                                   .String("component", "Component name, or custom:<id> for a scene-defined component.")
                                                                   .Integer("messages")
                                                                   .Integer("bytes")
                                                                   .Number("perTick", "messages / sceneTicks; 1.0 means written every tick (verdicts treat 95% of ticks as every tick).")
                                                                   .Integer("applied")
                                                                   .Integer("wasted")
                                                                   .String("verdict"),
                              "Top writers by message count.");

        private static JObject DirectionJson(in CrdtTrafficProbe.OutcomeCounts counts, long bytes, long ticks, List<CrdtTrafficProbe.EntrySnapshot> entries, int limit,
            Dictionary<int, EntityInfo> entities)
        {
            var writers = new JArray();
            int shown = Math.Min(limit, entries.Count);

            for (var i = 0; i < shown; i++)
                writers.Add(WriterJson(entries[i], ticks, entities));

            return new JObject
            {
                ["messages"] = counts.Total,
                ["bytes"] = bytes,
                ["perTick"] = Round1(PerTick(counts.Total, ticks)),
                ["applied"] = counts[CrdtTrafficOutcome.Applied],
                ["wasted"] = counts.Wasted,
                ["redundantIdenticalData"] = counts[CrdtTrafficOutcome.RedundantIdenticalData],
                ["noOpSameState"] = counts[CrdtTrafficOutcome.NoOpSameState],
                ["noOpOutdated"] = counts[CrdtTrafficOutcome.NoOpOutdated],
                ["noOpEntityDeleted"] = counts[CrdtTrafficOutcome.NoOpEntityDeleted],
                ["filteredCreatorHub"] = counts[CrdtTrafficOutcome.FilteredCreatorHub],
                ["distinctWriters"] = entries.Count,
                ["hotWriters"] = writers,
            };
        }

        private static JObject WriterJson(in CrdtTrafficProbe.EntrySnapshot entry, long ticks, Dictionary<int, EntityInfo> entities)
        {
            entities.TryGetValue(entry.EntityId, out EntityInfo info);

            return new JObject
            {
                ["entity"] = entry.EntityId,
                ["engineEntityId"] = info.EngineId.HasValue ? new JValue(info.EngineId.Value) : JValue.CreateNull(),
                ["entityLabel"] = info.Label != null ? new JValue(info.Label) : JValue.CreateNull(),
                ["componentId"] = entry.ComponentId,
                ["component"] = ComponentName(entry.ComponentId),
                ["messages"] = entry.Messages,
                ["bytes"] = entry.Bytes,
                ["perTick"] = Round1(PerTick(entry.Messages, ticks)),
                ["applied"] = entry.Counts[CrdtTrafficOutcome.Applied],
                ["wasted"] = entry.Counts.Wasted,
                ["verdict"] = Verdict(entry, ticks),
            };
        }

        private static void AppendDirectionText(StringBuilder text, string title, in CrdtTrafficProbe.OutcomeCounts counts, long bytes, long ticks,
            List<CrdtTrafficProbe.EntrySnapshot> entries, int limit, Dictionary<int, EntityInfo> entities)
        {
            text.Append(title).Append(": ").Append(counts.Total).Append(" msgs (")
                .Append(PerTick(counts.Total, ticks).ToString("F1", CultureInfo.InvariantCulture)).Append("/tick), ")
                .Append(bytes).Append(" bytes; applied ").Append(counts[CrdtTrafficOutcome.Applied]).Append(", wasted ").Append(counts.Wasted);

            if (counts.Wasted > 0)
            {
                text.Append(" (identical rewrites ").Append(counts[CrdtTrafficOutcome.RedundantIdenticalData])
                    .Append(", same state ").Append(counts[CrdtTrafficOutcome.NoOpSameState])
                    .Append(", outdated ").Append(counts[CrdtTrafficOutcome.NoOpOutdated])
                    .Append(", deleted entity ").Append(counts[CrdtTrafficOutcome.NoOpEntityDeleted])
                    .Append(", Creator Hub ").Append(counts[CrdtTrafficOutcome.FilteredCreatorHub]).Append(')');
            }

            text.AppendLine(".");

            int shown = Math.Min(limit, entries.Count);

            for (var i = 0; i < shown; i++)
            {
                CrdtTrafficProbe.EntrySnapshot entry = entries[i];
                entities.TryGetValue(entry.EntityId, out EntityInfo info);

                text.Append("  entity ").Append(entry.EntityId);

                if (info.EngineId.HasValue)
                    text.Append(" [engine ").Append(info.EngineId.Value).Append(']');

                if (info.Label != null)
                    text.Append(" (").Append(info.Label).Append(')');

                text.Append(' ').Append(ComponentName(entry.ComponentId)).Append(": ").Append(entry.Messages).Append(" msgs (")
                    .Append(PerTick(entry.Messages, ticks).ToString("F1", CultureInfo.InvariantCulture)).Append("/tick), ")
                    .Append(entry.Bytes).Append(" bytes - ").AppendLine(Verdict(entry, ticks));
            }

            if (entries.Count > shown)
                text.Append("  ... ").Append(entries.Count - shown).AppendLine(" more writers (raise limit).");
        }

        private static string Verdict(in CrdtTrafficProbe.EntrySnapshot entry, long ticks)
        {
            CrdtTrafficProbe.OutcomeCounts counts = entry.Counts;
            bool everyTick = entry.Messages >= ticks * EVERY_TICK_RATIO;

            if (counts.Wasted == 0)
                return everyTick ? "hot: changes every tick (applied; check whether every tick is needed)" : "ok";

            if (counts[CrdtTrafficOutcome.FilteredCreatorHub] == entry.Messages)
                return "wasted: Creator Hub component leaked from main.composite, never reaches the scene state";

            if (counts[CrdtTrafficOutcome.RedundantIdenticalData] == entry.Messages)
                return everyTick
                    ? "wasted: rewrites identical values every tick (re-set without a change)"
                    : "wasted: rewrites identical values";

            if (counts.Wasted == entry.Messages)
                return $"wasted: every message changed nothing ({counts[CrdtTrafficOutcome.RedundantIdenticalData]} identical rewrites, "
                       + $"{counts.Wasted - counts[CrdtTrafficOutcome.RedundantIdenticalData]} stale, duplicate, deleted-entity or Creator Hub)";

            return everyTick
                ? $"mixed: written every tick, {counts.Wasted} of {entry.Messages} wasted"
                : $"mixed: {counts.Wasted} of {entry.Messages} wasted";
        }

        private static string ComponentName(int componentId) =>
            COMPONENT_NAMES.TryGetValue(componentId, out string? name) ? name : $"custom:{componentId}";

        /// <summary>
        ///     Resolves the engine entity and a short description for every writer the report lists. Runs on the
        ///     main thread without the scene world's sync lock, like the existing debug tooling, so labels are a diagnostic snapshot.
        /// </summary>
        private static Dictionary<int, EntityInfo> ResolveEntities(World world, List<CrdtTrafficProbe.EntrySnapshot> fromScene,
            List<CrdtTrafficProbe.EntrySnapshot> toScene, int limit)
        {
            var wanted = new HashSet<int>();

            for (var i = 0; i < Math.Min(limit, fromScene.Count); i++)
                wanted.Add(fromScene[i].EntityId);

            for (var i = 0; i < Math.Min(limit, toScene.Count); i++)
                wanted.Add(toScene[i].EntityId);

            var engineEntities = new Dictionary<int, Entity>(wanted.Count);

            world.Query(in CRDT_ENTITIES, (Entity entity, ref CRDTEntity crdtEntity) =>
            {
                if (wanted.Contains(crdtEntity.Id) && !SceneEntityLabel.IsSceneContainer(world, entity, crdtEntity.Id))
                    engineEntities[crdtEntity.Id] = entity;
            });

            var entities = new Dictionary<int, EntityInfo>(wanted.Count);
            var parts = new List<string>(5);

            foreach (int entityId in wanted)
            {
                bool resolved = engineEntities.TryGetValue(entityId, out Entity entity);
                string? label = SceneEntityLabel.Reserved(entityId) ?? (resolved ? SceneEntityLabel.Describe(world, entity, parts) : null);
                entities[entityId] = new EntityInfo(resolved ? entity.Id : null, label);
            }

            return entities;
        }

        private static JObject BridgeTimeJson(in CrdtTrafficProbe.BridgeTime time, long sceneTicks) =>
            new ()
            {
                ["totalMs"] = Math.Round(TicksToMs(time.TotalTicks), 2),
                ["totalUsPerTick"] = Math.Round(PerTickUs(time.TotalTicks, sceneTicks), 1),
                ["incomingUsPerTick"] = Math.Round(PerTickUs(time.IncomingTicks, sceneTicks), 1),
                ["applyUsPerTick"] = Math.Round(PerTickUs(time.ApplyTicks, sceneTicks), 1),
                ["outgoingUsPerTick"] = Math.Round(PerTickUs(time.OutgoingTicks, sceneTicks), 1),
            };

        private static double TicksToMs(long stopwatchTicks) =>
            stopwatchTicks * 1000d / System.Diagnostics.Stopwatch.Frequency;

        private static double PerTickUs(long stopwatchTicks, long sceneTicks) =>
            sceneTicks > 0 ? stopwatchTicks * 1_000_000d / System.Diagnostics.Stopwatch.Frequency / sceneTicks : 0d;

        private static string FormatUs(double microseconds) =>
            microseconds.ToString("F1", CultureInfo.InvariantCulture);

        private static float PerTick(long messages, long ticks) =>
            ticks > 0 ? (float)messages / ticks : 0f;

        private static float Round1(float value) =>
            Mathf.Round(value * 10f) / 10f;

        private static UniTask WaitRealtimeAsync(float seconds, CancellationToken ct) =>
            UniTask.Delay(TimeSpan.FromSeconds(seconds), DelayType.Realtime, PlayerLoopTiming.Update, ct);

        /// <summary>
        ///     Maps the generated <see cref="ComponentID" /> constants to PascalCase names (GLTF_CONTAINER -> GltfContainer).
        /// </summary>
        private static Dictionary<int, string> BuildComponentNames()
        {
            FieldInfo[] fields = typeof(ComponentID).GetFields(BindingFlags.Public | BindingFlags.Static);
            var names = new Dictionary<int, string>(fields.Length);
            var builder = new StringBuilder();

            foreach (FieldInfo field in fields)
            {
                if (!field.IsLiteral || field.GetRawConstantValue() is not int componentId) continue;

                builder.Clear();
                var upper = true;

                foreach (char c in field.Name)
                {
                    if (c == '_')
                    {
                        upper = true;
                        continue;
                    }

                    builder.Append(upper ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c));
                    upper = false;
                }

                names[componentId] = builder.ToString();
            }

            return names;
        }

        private readonly struct EntityInfo
        {
            public readonly int? EngineId;
            public readonly string? Label;

            public EntityInfo(int? engineId, string? label)
            {
                EngineId = engineId;
                Label = label;
            }
        }

        /// <summary>
        ///     Shape of <see cref="WaitRealtimeAsync" />, the hold that spans the capture window. Injectable so tests
        ///     can feed the probe instead of waiting.
        /// </summary>
        public delegate UniTask WaitForWindow(float seconds, CancellationToken ct);
    }
}
