using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using ECS.SceneLifeCycle;
using Newtonsoft.Json.Linq;
using SceneRunner.Scene;
using System;
using System.Linq;
using UnityEngine.Networking;

namespace DCL.MCP.Handlers
{
    /// <summary>
    ///     Обработчик MCP для выдачи исходников главного JS файла сцены (текст)
    /// </summary>
    public class MCPSceneCodeHandler
    {
        private readonly IScenesCache scenesCache;

        public MCPSceneCodeHandler(IScenesCache scenesCache)
        {
            this.scenesCache = scenesCache;
        }

        /// <summary>
        ///     Получить текст главного JS файла (metadata.main). Если sceneId не указан — текущая сцена
        ///     params: { sceneId?: string, mode?: "lifecycle" }
        /// </summary>
        public async UniTask<object> HandleGetSceneMainJsAsync(JObject parameters)
        {
            try
            {
                if (!TryResolveScene(parameters, out ISceneFacade? targetScene, out string error))
                    return new { success = false, error };

                ISceneData sceneData = targetScene.SceneData;
                SceneEntityDefinition def = sceneData.SceneEntityDefinition;

                string mainPath = def.metadata?.main;

                if (string.IsNullOrEmpty(mainPath))
                    return new { success = false, sceneId = def.id, error = "Scene metadata.main is empty" };

                if (!sceneData.TryGetContentUrl(mainPath, out URLAddress mainUrl))
                    return new { success = false, sceneId = def.id, file = mainPath, error = "main file not found in scene content" };

                using var request = UnityWebRequest.Get(mainUrl.Value);
                await request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                    return new
                    {
                        success = false, sceneId = def.id, file = mainPath, url = mainUrl.Value,
                        request.error,
                    };

                string code = request.downloadHandler.text;

                string mode = parameters["mode"]?.ToString() ?? "lifecycle";

                if (mode == "lifecycle")
                {
                    string onStart = ExtractFunctionBody(code, "onStart");
                    string onUpdate = ExtractFunctionBody(code, "onUpdate");

                    return new
                    {
                        success = true,
                        sceneId = def.id,
                        file = mainPath,
                        url = mainUrl.Value,
                        onStart,
                        onUpdate,
                    };
                }

                // Fallback: always lifecycle-only to avoid huge payloads
                return new { success = true, sceneId = def.id, file = mainPath, url = mainUrl.Value, onStart = ExtractFunctionBody(code, "onStart"), onUpdate = ExtractFunctionBody(code, "onUpdate") };
            }
            catch (Exception e)
            {
                ReportHub.LogError(ReportCategory.DEBUG, $"[MCP Scene Code] getSceneMainJs failed: {e.Message}");
                return new { success = false, error = e.Message };
            }
        }

        /// <summary>
        ///     Пытается извлечь тело функции по ключу свойства (например, onStart/onUpdate) из минифицированного бандла.
        ///     Стратегия: ищем ключ, затем ближайшую '{' и балансируем скобки до соответствующей '}'.
        ///     Это эвристика и может вернуть пустую строку, если сигнатура сильно отличается.
        /// </summary>
        private static string ExtractFunctionBody(string code, string propertyName)
        {
            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(propertyName))
                return string.Empty;

            int keyIdx = code.IndexOf(propertyName, StringComparison.Ordinal);

            if (keyIdx < 0)
                return string.Empty;

            // Найти первую '{' после упоминания свойства (для тела функции)
            int braceStart = code.IndexOf('{', keyIdx);

            if (braceStart < 0)
                return string.Empty;

            var depth = 0;

            for (int i = braceStart; i < code.Length; i++)
            {
                char c = code[i];

                if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;

                    if (depth == 0)
                    {
                        // Включаем обрамляющие фигурные скобки
                        return code.Substring(braceStart, i - braceStart + 1);
                    }
                }
            }

            return string.Empty;
        }

        /// <summary>
        ///     Информация о sourcemap главного скрипта (если существует)
        ///     params: { sceneId?: string }
        /// </summary>
        public async UniTask<object> HandleGetSceneMainSourceMapInfoAsync(JObject parameters)
        {
            try
            {
                if (!TryResolveScene(parameters, out ISceneFacade? targetScene, out string error))
                    return new { success = false, error };

                ISceneData sceneData = targetScene.SceneData;
                SceneEntityDefinition def = sceneData.SceneEntityDefinition;

                string mainPath = def.metadata?.main;

                if (string.IsNullOrEmpty(mainPath))
                    return new { success = false, sceneId = def.id, error = "Scene metadata.main is empty" };

                string mapPath = mainPath + ".map";

                if (!sceneData.TryGetContentUrl(mapPath, out URLAddress mapUrl))
                    return new { success = false, sceneId = def.id, mainFile = mainPath, error = "Source map not found" };

                // fetch map text
                using var request = UnityWebRequest.Get(mapUrl.Value);
                await request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                    return new
                    {
                        success = false, sceneId = def.id, mainFile = mainPath, mapFile = mapPath, url = mapUrl.Value,
                        request.error,
                    };

                string mapText = request.downloadHandler.text;
                var map = JObject.Parse(mapText);

                string[] sources = map["sources"]?.ToObject<string[]>() ?? Array.Empty<string>();
                string[] sourcesContent = map["sourcesContent"]?.ToObject<string[]>() ?? Array.Empty<string>();

                // limit preview list
                string[] preview = sources.Take(20).ToArray();

                return new
                {
                    success = true,
                    sceneId = def.id,
                    mainFile = mainPath,
                    mapFile = mapPath,
                    url = mapUrl.Value,
                    sourcesCount = sources.Length,
                    hasSourcesContent = sourcesContent.Length == sources.Length && sources.Length > 0,
                    sourcesPreview = preview,
                };
            }
            catch (Exception e)
            {
                ReportHub.LogError(ReportCategory.DEBUG, $"[MCP Scene Code] getSceneMainSourceMapInfo failed: {e.Message}");
                return new { success = false, error = e.Message };
            }
        }

        /// <summary>
        ///     Извлечь исходник конкретного файла из sourcesContent по имени (как в поле sources[])
        ///     params: { sceneId?: string, source: string }
        /// </summary>
        public async UniTask<object> HandleGetSceneSourceFromMapAsync(JObject parameters)
        {
            try
            {
                if (!TryResolveScene(parameters, out ISceneFacade? targetScene, out string error))
                    return new { success = false, error };

                var requested = parameters["source"]?.ToString();

                if (string.IsNullOrEmpty(requested))
                    return new { success = false, error = "source parameter is required" };

                ISceneData sceneData = targetScene.SceneData;
                SceneEntityDefinition def = sceneData.SceneEntityDefinition;

                string mainPath = def.metadata?.main;

                if (string.IsNullOrEmpty(mainPath))
                    return new { success = false, sceneId = def.id, error = "Scene metadata.main is empty" };

                string mapPath = mainPath + ".map";

                if (!sceneData.TryGetContentUrl(mapPath, out URLAddress mapUrl))
                    return new { success = false, sceneId = def.id, mainFile = mainPath, error = "Source map not found" };

                using var request = UnityWebRequest.Get(mapUrl.Value);
                await request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                    return new
                    {
                        success = false, sceneId = def.id, mainFile = mainPath, mapFile = mapPath, url = mapUrl.Value,
                        request.error,
                    };

                string mapText = request.downloadHandler.text;
                var map = JObject.Parse(mapText);

                string[] sources = map["sources"]?.ToObject<string[]>() ?? Array.Empty<string>();
                string[] sourcesContent = map["sourcesContent"]?.ToObject<string[]>() ?? Array.Empty<string>();

                for (var i = 0; i < sources.Length; i++)
                {
                    if (string.Equals(sources[i], requested, StringComparison.Ordinal))
                    {
                        string content = i < sourcesContent.Length ? sourcesContent[i] : string.Empty;

                        if (string.IsNullOrEmpty(content))
                            return new { success = false, sceneId = def.id, source = requested, error = "sourcesContent not embedded in map" };

                        return new { success = true, sceneId = def.id, source = requested, length = content.Length, code = content };
                    }
                }

                return new { success = false, sceneId = def.id, source = requested, error = "source not found in map" };
            }
            catch (Exception e)
            {
                ReportHub.LogError(ReportCategory.DEBUG, $"[MCP Scene Code] getSceneSourceFromMap failed: {e.Message}");
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
    }
}
