using Arch.Core;
using CrdtEcsBridge.PoolsProviders;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using ECS.SceneLifeCycle;
using Newtonsoft.Json.Linq;
using SceneRunner.Scene;
using SceneRuntime.Apis.Modules.EngineApi;
using DCL.MCP.Systems;
using System;
using System.Linq;
using UnityEngine;

namespace DCL.MCP.Handlers
{
    /// <summary>
    ///     Обработчик команд получения информации о JS сценах через MCP
    /// </summary>
    public class MCPSceneInfoHandler
    {
        private readonly World globalWorld;
        private readonly IScenesCache scenesCache;

        public MCPSceneInfoHandler(World globalWorld, IScenesCache scenesCache)
        {
            this.globalWorld = globalWorld;
            this.scenesCache = scenesCache;
        }

        /// <summary>
        ///     Получить информацию обо всех загруженных сценах
        /// </summary>
        public async UniTask<object> HandleGetAllScenesInfoAsync(JObject parameters)
        {
            try
            {
                var scenes = scenesCache.Scenes.Select(scene => new
                                         {
                                             sceneId = scene.SceneData?.SceneEntityDefinition?.id ?? "unknown",
                                             name = scene.Info.Name,
                                             baseParcel = $"({scene.Info.BaseParcel.x},{scene.Info.BaseParcel.y})",
                                             parcelsCount = scene.SceneData?.Parcels?.Count ?? 0,
                                             isCurrentScene = scenesCache.CurrentScene == scene,
                                             isReady = scene.IsSceneReady(),
                                             isPortableExperience = scene.SceneData?.IsPortableExperience() ?? false,
                                             isSdk7 = scene.SceneData?.IsSdk7() ?? false,
                                         })
                                        .ToList();

                var portableExperiences = scenesCache.PortableExperiencesScenes.Select(scene => new
                                                      {
                                                          sceneId = scene.SceneData?.SceneEntityDefinition?.id ?? "unknown",
                                                          name = scene.Info.Name,
                                                          isPortableExperience = true,
                                                          isSdk7 = scene.SceneData?.IsSdk7() ?? false,
                                                      })
                                                     .ToList();

                Vector2Int currentParcel = scenesCache.CurrentParcel.Value;

                ReportHub.Log(ReportCategory.DEBUG, $"[MCP Scene Info] Got info for {scenes.Count} scenes and {portableExperiences.Count} portable experiences");

                return new
                {
                    success = true,
                    scenes,
                    portableExperiences,
                    currentParcel = $"({currentParcel.x},{currentParcel.y})",
                    totalScenes = scenes.Count,
                    totalPortableExperiences = portableExperiences.Count,
                };
            }
            catch (Exception e)
            {
                ReportHub.LogError(ReportCategory.DEBUG, $"[MCP Scene Info] getAllScenesInfo failed: {e.Message}");
                return new { success = false, error = e.Message };
            }
        }

        /// <summary>
        ///     Получить детальную информацию о конкретной сцене
        /// </summary>
        public async UniTask<object> HandleGetSceneInfoAsync(JObject parameters)
        {
            try
            {
                var sceneId = parameters["sceneId"]?.ToString();

                if (string.IsNullOrEmpty(sceneId))
                {
                    return new
                    {
                        success = false,
                        error = "sceneId parameter is required",
                    };
                }

                // Поиск сцены по ID
                ISceneFacade? targetScene = null;

                foreach (ISceneFacade scene in scenesCache.Scenes)
                {
                    if (scene.SceneData?.SceneEntityDefinition?.id == sceneId)
                    {
                        targetScene = scene;
                        break;
                    }
                }

                // Проверяем также portable experiences
                if (targetScene == null)
                {
                    foreach (ISceneFacade scene in scenesCache.PortableExperiencesScenes)
                    {
                        if (scene.SceneData?.SceneEntityDefinition?.id == sceneId)
                        {
                            targetScene = scene;
                            break;
                        }
                    }
                }

                if (targetScene == null)
                {
                    return new
                    {
                        success = false,
                        error = $"Scene with id '{sceneId}' not found",
                    };
                }

                ISceneData sceneData = targetScene.SceneData;
                SceneEntityDefinition definition = sceneData.SceneEntityDefinition;
                SceneMetadata metadata = definition.metadata;

                // Собираем parcels если есть
                var parcels = sceneData.Parcels?.Select(p => $"({p.x},{p.y})").ToList();

                // Собираем spawn points если есть
                var spawnPoints = metadata.spawnPoints?.Select(sp =>
                                           {
                                               var pos = sp.position.ToVector3();

                                               return new
                                               {
                                                   sp.name,
                                                   isDefault = sp.@default,
                                                   position = new
                                                   {
                                                       pos.x,
                                                       pos.y,
                                                       pos.z,
                                                   },
                                               };
                                           })
                                          .ToList();

                ReportHub.Log(ReportCategory.DEBUG, $"[MCP Scene Info] Got detailed info for scene {sceneId}");

                return new
                {
                    success = true,
                    sceneId = definition.id,
                    metadata = new
                    {
                        name = metadata.scene?.DecodedBase.ToString() ?? "Unknown",
                        metadata.main,
                        metadata.runtimeVersion,
                        parcels,
                        spawnPoints,
                        metadata.requiredPermissions,
                        metadata.allowedMediaHostnames,
                        metadata.isPortableExperience,
                    },
                    info = new
                    {
                        baseParcel = $"({targetScene.Info.BaseParcel.x},{targetScene.Info.BaseParcel.y})",
                        isReady = targetScene.IsSceneReady(),
                        isSdk7 = sceneData.IsSdk7(),
                        isCurrentScene = scenesCache.CurrentScene == targetScene,
                    },
                };
            }
            catch (Exception e)
            {
                ReportHub.LogError(ReportCategory.DEBUG, $"[MCP Scene Info] getSceneInfo failed: {e.Message}");
                return new { success = false, error = e.Message };
            }
        }

        /// <summary>
        ///     Получить CRDT состояние конкретной сцены (сериализованное в Base64)
        /// </summary>
        public async UniTask<object> HandleGetSceneCrdtStateAsync(JObject parameters)
        {
            try
            {
                // Всегда работаем с текущей сценой
                ISceneFacade? targetScene = scenesCache.CurrentScene;

                if (targetScene == null)
                {
                    return new
                    {
                        success = false,
                        error = "No current scene available",
                    };
                }

                // Получаем EngineAPI по сцене
                if (!EngineApiLocator.TryGet(targetScene.Info, out IEngineApi engineApi))
                {
                    return new
                    {
                        success = false,
                        sceneId = targetScene.SceneData?.SceneEntityDefinition?.id ?? "unknown",
                        error = "EngineApi not available for target scene",
                    };
                }

                // Снимок CRDT и экспорт в JSON-структуру
                PoolableByteArray data = engineApi.CrdtGetState();

                try
                {
                    string json = SceneStateJsonExporter.ExportStateToJson(data);
                    var state = JObject.Parse(json);

                    ReportHub.Log(ReportCategory.DEBUG, $"[MCP Scene Info] CRDT state exported for current scene {targetScene.SceneData?.SceneEntityDefinition?.id ?? "unknown"}");

                    return new
                    {
                        success = true,
                        sceneId = targetScene.SceneData?.SceneEntityDefinition?.id ?? "unknown",
                        state,
                    };
                }
                finally { data.Dispose(); }
            }
            catch (Exception e)
            {
                ReportHub.LogError(ReportCategory.DEBUG, $"[MCP Scene Info] getSceneCrdtState failed: {e.Message}");
                return new { success = false, error = e.Message };
            }
        }
    }
}
