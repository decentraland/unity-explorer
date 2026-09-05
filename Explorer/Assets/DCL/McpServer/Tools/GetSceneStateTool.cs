using Cysharp.Threading.Tasks;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using DCL.RealmNavigation;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.CurrentScene;
using Newtonsoft.Json.Linq;
using SceneRunner.Scene;
using System.Threading;
using UnityEngine;

namespace DCL.McpServer.Tools
{
    public class GetSceneStateTool : McpTool
    {
        private readonly IScenesCache scenesCache;
        private readonly ICurrentSceneInfo currentSceneInfo;
        private readonly ILoadingStatus loadingStatus;
        private readonly bool localSceneDevelopment;

        public override string Name => "get_scene_state";

        public override string Description =>
            "Read the state of the scene at the player's current parcel: name, base parcel, runtime state (including JavaScript/ECS errors), "
            + "readiness, asset loading progress and the global loading-screen stage. Call this after teleporting or reloading before interacting.";

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
                                                         .String("runningStatus"),
                              "The scene at the player's current parcel, or null when no scene is loaded there.", nullable: true)
                          .Build();

        public override McpToolAnnotations Annotations => McpToolAnnotations.ReadOnly();

        public GetSceneStateTool(IScenesCache scenesCache, ICurrentSceneInfo currentSceneInfo, ILoadingStatus loadingStatus, bool localSceneDevelopment)
        {
            this.scenesCache = scenesCache;
            this.currentSceneInfo = currentSceneInfo;
            this.loadingStatus = loadingStatus;
            this.localSceneDevelopment = localSceneDevelopment;
        }

        public override UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            Vector2Int currentParcel = scenesCache.CurrentParcel.Value;
            ISceneFacade? scene = scenesCache.CurrentScene.Value;

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
                    },
            };

            return UniTask.FromResult(McpToolResult.JsonWithStructured(state));
        }
    }
}
