using CRDT.Attribution;
using Cysharp.Threading.Tasks;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using DCL.RealmNavigation;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.CurrentScene;
using Newtonsoft.Json.Linq;
using SceneRunner.Scene;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.McpServer.Tools
{
    public class GetSceneStateTool : McpTool
    {
        private readonly IScenesCache scenesCache;
        private readonly ICurrentSceneInfo currentSceneInfo;
        private readonly ILoadingStatus loadingStatus;
        private readonly ICrdtWriterLog writerLog;
        private readonly bool localSceneDevelopment;

        private readonly List<CrdtWriterSummary> writersBuffer = new ();

        public override string Name => "get_scene_state";

        public override string Description =>
            "Read the state of the scene at the player's current parcel: name, base parcel, runtime state (including JavaScript/ECS errors), "
            + "readiness, asset loading progress and the global loading-screen stage. Call this after teleporting or reloading before interacting. "
            + "For a networked scene it also reports which addresses have written to its state — for an authoritative game "
            + "(authoritativeMultiplayer), an address other than 'authoritative-server' with a non-zero 'writes' count is a peer "
            + "asserting state the server did not.";

        public override JObject OutputSchema =>
            McpJsonSchema.Object()
                          .Object("currentParcel", JObjectExtensions.ParcelSchema())
                          .String("loadingStage")
                          .Boolean("loadingScreenOn")
                          .Boolean("localSceneDevelopment")
                          .Object("scene", McpJsonSchema.Object()
                                                         .String("name")
                                                         .String("sceneId", "Scene definition id; pass it to click_entity's sceneId to pin clicks to this scene. Null when unknown.", nullable: true)
                                                         .Object("baseParcel", JObjectExtensions.ParcelSchema())
                                                         .String("sdkVersion", "SDK version reported by the scene, or null when unknown.", nullable: true)
                                                         .String("state")
                                                         .Boolean("isReady")
                                                         .Boolean("assetsLoadingConcluded")
                                                         .String("runningStatus")
                                                         .Boolean("authoritativeMultiplayer", "True when the scene declares an authoritative server as the only legitimate author of its synced state.")
                                                         .ObjectArray("networkWriters", CrdtAttributionJson.WriterSchema(),
                                                              "Addresses that have written to this scene's synced state since this client started observing, most recent first. "
                                                              + "Empty for a scene nobody writes to over the scene room; writes the scene's own code makes locally never appear here.")
                                                         .Integer("droppedWriteRecords", "Writes the client could not record because a per-scene budget was full (4096 components or 128 distinct addresses); above zero, both networkWriters and get_entity_details undercount."),
                              "The scene at the player's current parcel, or null when no scene is loaded there.", nullable: true)
                          .Build();

        public override McpToolAnnotations Annotations => McpToolAnnotations.ReadOnly();

        public GetSceneStateTool(IScenesCache scenesCache, ICurrentSceneInfo currentSceneInfo, ILoadingStatus loadingStatus, ICrdtWriterLog writerLog, bool localSceneDevelopment)
        {
            this.scenesCache = scenesCache;
            this.currentSceneInfo = currentSceneInfo;
            this.loadingStatus = loadingStatus;
            this.writerLog = writerLog;
            this.localSceneDevelopment = localSceneDevelopment;
        }

        public override UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            Vector2Int currentParcel = scenesCache.CurrentParcel.Value;
            ISceneFacade? scene = scenesCache.CurrentScene.Value;

            writersBuffer.Clear();
            var droppedWriteRecords = 0;

            if (scene?.SceneData.SceneEntityDefinition.id is { } sceneId)
            {
                writerLog.SceneWriters(sceneId, writersBuffer);
                writersBuffer.Sort(static (left, right) => left.LastWriteAgeSeconds.CompareTo(right.LastWriteAgeSeconds));
                droppedWriteRecords = writerLog.DroppedWrites(sceneId);
            }

            var state = new JObject
            {
                ["currentParcel"] = currentParcel.ToParcel(),
                ["loadingStage"] = loadingStatus.CurrentStage.Value.ToString(),
                ["loadingScreenOn"] = loadingStatus.IsLoadingScreenOn(),
                ["localSceneDevelopment"] = localSceneDevelopment,
                ["scene"] = scene == null
                    ? JValue.CreateNull()
                    : new JObject
                    {
                        ["name"] = scene.Info.Name,
                        ["sceneId"] = scene.SceneData.SceneEntityDefinition.id,
                        ["baseParcel"] = scene.Info.BaseParcel.ToParcel(),
                        ["sdkVersion"] = scene.Info.SdkVersion,
                        ["state"] = scene.SceneStateProvider.State.Value().ToString(),
                        ["isReady"] = scene.IsSceneReady(),
                        ["assetsLoadingConcluded"] = scene.SceneData.SceneLoadingConcluded,
                        ["runningStatus"] = currentSceneInfo.SceneStatus.Value?.ToString() ?? "Unknown",
                        ["authoritativeMultiplayer"] = scene.SceneData.SceneEntityDefinition.metadata?.authoritativeMultiplayer ?? false,
                        ["networkWriters"] = CrdtAttributionJson.Writers(writersBuffer),
                        ["droppedWriteRecords"] = droppedWriteRecords,
                    },
            };

            return UniTask.FromResult(McpToolResult.JsonWithStructured(state));
        }
    }
}
