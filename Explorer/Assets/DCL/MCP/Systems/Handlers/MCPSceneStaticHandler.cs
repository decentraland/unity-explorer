using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using ECS.SceneLifeCycle;
using Newtonsoft.Json.Linq;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;

namespace DCL.MCP.Handlers
{
    /// <summary>
    ///     Обработчик MCP для выдачи статического содержимого сцены (контент из деплоя)
    /// </summary>
    public class MCPSceneStaticHandler
    {
        private readonly IScenesCache scenesCache;

        public MCPSceneStaticHandler(IScenesCache scenesCache)
        {
            this.scenesCache = scenesCache;
        }

        /// <summary>
        ///     Получить индекс контента (file/hash) и сырой metadata.json для текущей сцены (минимум, без дублирования)
        /// </summary>
        public async UniTask<object> HandleGetCurrentSceneStaticAsync(JObject parameters)
        {
            try
            {
                ISceneFacade? targetScene = scenesCache.CurrentScene;

                if (targetScene == null)
                    return new { success = false, error = "No current scene available" };

                return BuildStaticSceneMinimalPayload(targetScene.SceneData);
            }
            catch (Exception e)
            {
                ReportHub.LogError(ReportCategory.DEBUG, $"[MCP Scene Static] getCurrentSceneStatic failed: {e.Message}");
                return new { success = false, error = e.Message };
            }
        }

        /// <summary>
        ///     Получить индекс контента (file/hash) и сырой metadata.json по sceneId (URN/ENS)
        /// </summary>
        public async UniTask<object> HandleGetSceneStaticByIdAsync(JObject parameters)
        {
            try
            {
                var sceneId = parameters["sceneId"]?.ToString();

                if (string.IsNullOrEmpty(sceneId))
                    return new { success = false, error = "sceneId parameter is required" };

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
                    return new { success = false, error = $"Scene with id '{sceneId}' not found" };

                return BuildStaticSceneMinimalPayload(targetScene.SceneData);
            }
            catch (Exception e)
            {
                ReportHub.LogError(ReportCategory.DEBUG, $"[MCP Scene Static] getSceneStaticById failed: {e.Message}");
                return new { success = false, error = e.Message };
            }
        }

        /// <summary>
        ///     Получить только индекс контента (file/hash). Если sceneId не указан — используется текущая сцена
        /// </summary>
        public async UniTask<object> HandleGetSceneContentIndexAsync(JObject parameters)
        {
            try
            {
                if (!TryResolveScene(parameters, out ISceneFacade? targetScene, out string error))
                    return new { success = false, error };

                return BuildContentIndexPayload(targetScene.SceneData);
            }
            catch (Exception e)
            {
                ReportHub.LogError(ReportCategory.DEBUG, $"[MCP Scene Static] getSceneContentIndex failed: {e.Message}");
                return new { success = false, error = e.Message };
            }
        }

        /// <summary>
        ///     Получить только сырой JSON метаданных (scene.json) как строку. Если sceneId не указан — текущая сцена
        /// </summary>
        public async UniTask<object> HandleGetSceneMetadataJsonAsync(JObject parameters)
        {
            try
            {
                if (!TryResolveScene(parameters, out ISceneFacade? targetScene, out string error))
                    return new { success = false, error };

                SceneEntityDefinition def = targetScene.SceneData.SceneEntityDefinition;

                return new
                {
                    success = true,
                    sceneId = def.id,
                    metadataRawJson = def.metadata?.OriginalJson,
                };
            }
            catch (Exception e)
            {
                ReportHub.LogError(ReportCategory.DEBUG, $"[MCP Scene Static] getSceneMetadataJson failed: {e.Message}");
                return new { success = false, error = e.Message };
            }
        }

        /// <summary>
        ///     Разрешить URL конкретного файла из контента (например, bin/index.js). Если sceneId не указан — текущая сцена
        ///     params: { "file": "bin/index.js", "sceneId"?: string }
        /// </summary>
        public async UniTask<object> HandleGetSceneFileUrlAsync(JObject parameters)
        {
            try
            {
                var file = parameters["file"]?.ToString();

                if (string.IsNullOrEmpty(file))
                    return new { success = false, error = "file parameter is required" };

                if (!TryResolveScene(parameters, out ISceneFacade? targetScene, out string error))
                    return new { success = false, error };

                ISceneData sceneData = targetScene.SceneData;

                if (!sceneData.TryGetContentUrl(file, out URLAddress url))
                    return new { success = false, file, error = "file not found in scene content" };

                sceneData.TryGetHash(file, out string hash);

                return new
                {
                    success = true,
                    sceneId = sceneData.SceneEntityDefinition.id,
                    file,
                    url = url.Value,
                    hash,
                };
            }
            catch (Exception e)
            {
                ReportHub.LogError(ReportCategory.DEBUG, $"[MCP Scene Static] getSceneFileUrl failed: {e.Message}");
                return new { success = false, error = e.Message };
            }
        }

        private bool TryResolveScene(JObject parameters, out ISceneFacade? scene, out string error)
        {
            error = string.Empty;
            scene = null;

            var sceneId = parameters["sceneId"]?.ToString();

            if (string.IsNullOrEmpty(sceneId))
            {
                scene = scenesCache.CurrentScene;

                if (scene == null)
                {
                    error = "No current scene available";
                    return false;
                }

                return true;
            }

            foreach (ISceneFacade s in scenesCache.Scenes)
            {
                if (s.SceneData?.SceneEntityDefinition?.id == sceneId)
                {
                    scene = s;
                    return true;
                }
            }

            foreach (ISceneFacade s in scenesCache.PortableExperiencesScenes)
            {
                if (s.SceneData?.SceneEntityDefinition?.id == sceneId)
                {
                    scene = s;
                    return true;
                }
            }

            error = $"Scene with id '{sceneId}' not found";
            return false;
        }

        private static List<object> BuildContentList(ISceneData sceneData)
        {
            SceneEntityDefinition definition = sceneData.SceneEntityDefinition;

            // Собираем упрощённый список контента file/hash
            var contentList = new List<object>();

            if (definition.content != null)
            {
                foreach (ContentDefinition cd in definition.content)
                    contentList.Add(new
                    {
                        cd.file,
                        cd.hash,
                    });
            }

            return contentList;
        }

        private static object BuildContentIndexPayload(ISceneData sceneData)
        {
            List<object> contentList = BuildContentList(sceneData);
            SceneEntityDefinition definition = sceneData.SceneEntityDefinition;

            return new
            {
                success = true,
                sceneId = definition.id,
                baseUrl = sceneData.SceneContent.ContentBaseUrl.Value,
                content = contentList,
            };
        }

        private static object BuildStaticSceneMinimalPayload(ISceneData sceneData)
        {
            SceneEntityDefinition def = sceneData.SceneEntityDefinition;
            List<object> contentList = BuildContentList(sceneData);

            return new
            {
                success = true,
                sceneId = def.id,
                baseUrl = sceneData.SceneContent.ContentBaseUrl.Value,

                // сырой scene.json без распарсенных дублирующих полей
                metadataRawJson = def.metadata?.OriginalJson,

                // индекс контента
                content = contentList,
            };
        }
    }
}
