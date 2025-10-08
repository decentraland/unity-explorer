using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using ECS.SceneLifeCycle;
using Newtonsoft.Json.Linq;
using SceneRunner.Scene;
using System;

namespace DCL.MCP.Handlers
{
    /// <summary>
    ///     MVP-инжектор: добавляет JS логику в onUpdate через очередь исполнения в рантайме
    /// </summary>
    public class MCPSceneInjectionHandler
    {
        private readonly IScenesCache scenesCache;

        public MCPSceneInjectionHandler(IScenesCache scenesCache)
        {
            this.scenesCache = scenesCache;
        }

        /// <summary>
        ///     Простая проверка пути: создаёт текст через MCPSceneCreationSystem из Unity C#
        ///     params: { text: string, x?: number, y?: number, z?: number }
        /// </summary>
        public async UniTask<object> HandleDebugSpawnTextAsync(JObject parameters)
        {
            string text = parameters["text"]?.ToString() ?? "MCP DEBUG";
            float x = (float?)parameters["x"]?.ToObject<double>() ?? 8f;
            float y = (float?)parameters["y"]?.ToObject<double>() ?? 3f;
            float z = (float?)parameters["z"]?.ToObject<double>() ?? 8f;

            // MCPSceneCreationSystem.EnqueueSpawnText(text, new UnityEngine.Vector3(x, y, z));
            return new { success = true, text, position = new { x, y, z } };
        }

        /// <summary>
        ///     Инжект кода в onUpdate: оборачивает __internalScene.onUpdate = async (dt) => { /*code*/; await prev(dt) }
        ///     params: { sceneId?: string, code: string, position?: "before"|"after" }
        /// </summary>
        public async UniTask<object> HandleInjectSceneOnUpdateAsync(JObject parameters)
        {
            try
            {
                var code = parameters["code"]?.ToString();

                if (string.IsNullOrEmpty(code))
                    return new { success = false, error = "code parameter is required" };

                string position = parameters["position"]?.ToString() ?? "before";

                ISceneFacade? targetScene = ResolveScene(parameters);

                if (targetScene == null)
                    return new { success = false, error = "No current scene available" };

                var wrapped = $@"
                    (function() {{
                        if (typeof require !== 'function') {{ throw new Error('require not available'); }}
                        const ok = (typeof __internalScene !== 'undefined') && (__internalScene && typeof __internalScene.onUpdate === 'function');
                        if (!ok) {{ throw new Error('__internalScene.onUpdate not found'); }}
                        const __prevUpdate = __internalScene.onUpdate;
                        __internalScene.onUpdate = async function(dt) {{
                            {(position == "after" ? "await __prevUpdate(dt);" : "")}
                            {code}
                            {(position == "after" ? "" : "await __prevUpdate(dt);")}
                        }}
                    }})();
                ";

                // enqueue for evaluation via facade API
                targetScene.EnqueueJsEvaluation(wrapped);

                return new { success = true, sceneId = targetScene.SceneData.SceneEntityDefinition.id, position };
            }
            catch (Exception e)
            {
                ReportHub.LogError(ReportCategory.DEBUG, $"[MCP Scene Injection] injectSceneOnUpdate failed: {e.Message}");
                return new { success = false, error = e.Message };
            }
        }

        private ISceneFacade? ResolveScene(JObject parameters)
        {
            var sceneId = parameters["sceneId"]?.ToString();

            if (string.IsNullOrEmpty(sceneId))
                return scenesCache.CurrentScene;

            foreach (ISceneFacade s in scenesCache.Scenes)
                if (s.SceneData?.SceneEntityDefinition?.id == sceneId)
                    return s;

            foreach (ISceneFacade s in scenesCache.PortableExperiencesScenes)
                if (s.SceneData?.SceneEntityDefinition?.id == sceneId)
                    return s;

            return null;
        }
    }
}
