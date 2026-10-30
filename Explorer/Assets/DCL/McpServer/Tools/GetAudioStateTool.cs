using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using ECS.SceneLifeCycle;
using Newtonsoft.Json.Linq;
using SceneRunner.Scene;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using UnityEngine;

namespace DCL.McpServer.Tools
{
    /// <summary>
    ///     Reports every sound emitter of the current scene as the client runs it now — the audio channel an agent
    ///     has no ears for. Reads the scene world without its sync lock, like <see cref="ListSceneEntitiesTool" />.
    /// </summary>
    public class GetAudioStateTool : McpTool
    {
        private const int DEFAULT_LIMIT = 50;
        private const int MAX_LIMIT = 500;

        private const string NOT_CURRENT_NOTE =
            "the scene is not current: its audio is muted (volume forced to 0) until the player stands on it";

        private readonly IScenesCache scenesCache;
        private readonly World globalWorld;
        private readonly Entity playerEntity;

        public override string Name => "get_audio_state";

        public override string Description =>
            "List the current scene's sound emitters (AudioSource clips, AudioStream and VideoPlayer media) with their playback state "
            + "right now: playing or not, authored and live volume, loop, pitch, spatial range, position and distance from the player, "
            + "nearest first. Use it to verify a sound cue fired, loops, or is audible from where the player stands, since no screenshot "
            + "can. A snapshot read without the scene's sync lock: state is what the client runs this frame, not what the scene code "
            + "last asked for. A scene that is not current has its audio muted, which the note states.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.Boolean("onlyPlaying", "Only emitters currently playing. Default false.")
                  .Integer("limit", "Maximum entries to return. Default 50, max 500.");

        public override JObject OutputSchema =>
            McpJsonSchema.Object()
                          .Boolean("sceneIsCurrent", "False when the player is off the scene; its audio is muted then.")
                          .String("note", "Why the numbers may not be what the scene expects; null when nothing applies.", nullable: true)
                          .Object("summary", McpJsonSchema.Object()
                                                           .Integer("total", "Emitters in the scene, before onlyPlaying.")
                                                           .Integer("playing")
                                                           .Integer("audioSources")
                                                           .Integer("streams")
                                                           .Integer("videos"))
                          .Integer("returned")
                          .Boolean("truncated")
                          .ObjectArray("entries", McpJsonSchema.Object()
                                                                .Integer("entityId", "Engine entity id, for get_entity_details.")
                                                                .Integer("crdtEntity", "CRDT id the scene code uses; -1 when the entity has none.")
                                                                .Enum<AudioKind>("kind")
                                                                .String("source", "Clip url, stream url or video src.", nullable: true)
                                                                .Enum<AudioPlaybackState>("state", "not_created: the scene declared it but the client has not built the emitter yet.")
                                                                .Boolean("playing", "Whether the client's emitter is producing sound this frame.")
                                                                .Object("volume", McpJsonSchema.Object()
                                                                                               .Number("authored", "The scene's value.")
                                                                                               .Number("live", "The client's emitter volume (0 while the scene is not current); null before the emitter exists.", nullable: true))
                                                                .Boolean("loop")
                                                                .Number("pitch", "Pitch (AudioSource) or playback rate (VideoPlayer); null for streams.", nullable: true)
                                                                .Boolean("spatial", "False for a global (non-positional) emitter.")
                                                                .Number("minDistance", nullable: true)
                                                                .Number("maxDistance", "Metres beyond which a spatial emitter is inaudible.", nullable: true)
                                                                .Number("currentTime", "Seconds into the clip or media; null before it is loaded.", nullable: true)
                                                                .Number("duration", nullable: true)
                                                                .Boolean("liveStream")
                                                                .Object("position", JObjectExtensions.VectorSchema(), "World position; null when the entity has no transform.", nullable: true)
                                                                .Number("distance", "Metres from the player; null when the entity has no transform.", nullable: true),
                              "Emitters nearest the player first.")
                          .Build();

        public override McpToolAnnotations Annotations => McpToolAnnotations.ReadOnly();

        public GetAudioStateTool(IScenesCache scenesCache, World globalWorld, Entity playerEntity)
        {
            this.scenesCache = scenesCache;
            this.globalWorld = globalWorld;
            this.playerEntity = playerEntity;
        }

        public override UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            bool onlyPlaying = arguments.GetBool("onlyPlaying", false);
            int limit = Mathf.Clamp(arguments.GetInt("limit", DEFAULT_LIMIT), 1, MAX_LIMIT);

            ISceneFacade? scene = scenesCache.CurrentScene.Value;

            if (scene == null)
                return UniTask.FromResult(McpToolResult.Error("No scene world found at the current parcel."));

            Vector3 playerPosition = globalWorld.Get<CharacterTransform>(playerEntity).Position;
            var entries = new List<AudioEntry>();
            AudioStateReader.Collect(scene.EcsExecutor.World, playerPosition, entries);

            bool sceneIsCurrent = scene.SceneStateProvider.IsCurrent;
            JObject structured = AudioStateReader.Build(entries, onlyPlaying, limit, sceneIsCurrent, sceneIsCurrent ? null : NOT_CURRENT_NOTE);

            return UniTask.FromResult(McpToolResult.TextWithStructured(Describe(structured), structured));
        }

        private static string Describe(JObject structured)
        {
            var summary = (JObject)structured["summary"]!;
            var text = new StringBuilder();

            text.Append("emitters=").Append(summary["total"])
                .Append(" playing=").Append(summary["playing"])
                .Append(" returned=").Append(structured["returned"]);

            if (structured["note"] is { Type: JTokenType.String } note)
                text.Append(" — ").Append(note);

            text.AppendLine();

            foreach (JToken entry in (JArray)structured["entries"]!)
            {
                text.Append("crdt=").Append(entry["crdtEntity"])
                    .Append(' ').Append(entry["kind"])
                    .Append(' ').Append(entry["state"])
                    .Append(entry["playing"]!.Value<bool>() ? " playing" : string.Empty);

                if (entry["distance"] is { Type: JTokenType.Float } distance)
                    text.Append(' ').Append(distance.Value<double>().ToString("0.##", CultureInfo.InvariantCulture)).Append("m");

                if (entry["source"] is { Type: JTokenType.String } source)
                    text.Append(" (").Append(source).Append(')');

                text.AppendLine();
            }

            if (structured["truncated"]!.Value<bool>())
                text.Append("more emitters exist; raise limit (max ").Append(MAX_LIMIT).Append(") to see them.");

            return text.ToString();
        }
    }
}
