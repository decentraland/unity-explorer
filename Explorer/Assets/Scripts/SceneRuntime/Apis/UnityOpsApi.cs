using DCL.Diagnostics;
using JetBrains.Annotations;
using Microsoft.ClearScript.V8;
using SceneRuntime.ModuleHub;

namespace SceneRuntime.Apis
{
    public class UnityOpsApi
    {
        private readonly V8ScriptEngine engine;
        private readonly ISceneModuleHub moduleHub;
        private readonly V8Script sceneScript;
        private readonly SceneShortInfo sceneShortInfo;

        public UnityOpsApi(V8ScriptEngine engine, ISceneModuleHub moduleHub, V8Script sceneScript, SceneShortInfo sceneShortInfo)
        {
            this.engine = engine;
            this.moduleHub = moduleHub;
            this.sceneScript = sceneScript;
            this.sceneShortInfo = sceneShortInfo;
        }

        [UsedImplicitly]
        public void Log(object message)
        {
            ReportHub.Log(new ReportData(ReportCategory.JAVASCRIPT, sceneShortInfo: sceneShortInfo), message);
        }

        [UsedImplicitly]
        public void Warning(object message)
        {
            ReportHub.LogWarning(new ReportData(ReportCategory.JAVASCRIPT, sceneShortInfo: sceneShortInfo), message);
        }

        [UsedImplicitly]
        public void Error(object message)
        {
            ReportHub.LogError(
                new ReportData(ReportCategory.JAVASCRIPT, sceneShortInfo: sceneShortInfo),
                message + " stackTrace: " + engine.GetStackTrace()
            );
        }

        [UsedImplicitly]
        public object? LoadAndEvaluateCode(string moduleName) // "~system/EngineApi"
        {
            const char PATH_CHAR = '~';

            // Load just Scene Code
            if (moduleName == "~scene.js")
                return engine.Evaluate(sceneScript);

            if (moduleName.Length == 0)
            {
                ReportHub.LogWarning(new ReportData(ReportCategory.JAVASCRIPT, sceneShortInfo: sceneShortInfo), $"{moduleName} is not a module and won't be evaluated");
                return null;
            }

            string filename = moduleName.StartsWith(PATH_CHAR) ? moduleName.Substring(1) : moduleName;

            // Load JavaScript wrapper in the Runtime
            V8Script moduleScript = moduleHub.ModuleScript(filename);

            return engine.Evaluate(moduleScript);
        }
    }
}
