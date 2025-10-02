using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using ECS.SceneLifeCycle;
using Newtonsoft.Json.Linq;
using SceneRunner.Scene;
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

                // Получаем CRDT state через EcsExecutor
                // Это вызовет метод CrdtGetState() в EngineAPIImplementation
                // TODO: Нужно найти способ получить сериализованное состояние CRDT
                // Для MVP возвращаем информацию о том, что функция в разработке

                ReportHub.LogWarning(ReportCategory.DEBUG, $"[MCP Scene Info] CRDT state extraction for scene {sceneId} - not yet implemented in MVP");

                return new
                {
                    success = false,
                    error = "CRDT state extraction not yet implemented in MVP. This requires deeper integration with EngineAPIImplementation.",
                    sceneId,
                    note = "This feature needs access to scene's CRDTProtocol instance which is internal to the scene runtime.",
                };
            }
            catch (Exception e)
            {
                ReportHub.LogError(ReportCategory.DEBUG, $"[MCP Scene Info] getSceneCrdtState failed: {e.Message}");
                return new { success = false, error = e.Message };
            }
        }
    }
}
