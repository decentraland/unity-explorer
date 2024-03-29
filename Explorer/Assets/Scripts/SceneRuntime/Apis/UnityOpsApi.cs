using DCL.Diagnostics;
using JetBrains.Annotations;
using Microsoft.ClearScript.V8;

namespace SceneRuntime.Apis
{
    public class UnityOpsApi
    {
        private readonly V8ScriptEngine engine;
        private readonly SceneModuleHub moduleHub;
        private readonly V8Script sceneScript;
        private readonly SceneShortInfo sceneShortInfo;

        public UnityOpsApi(V8ScriptEngine engine, SceneModuleHub moduleHub, V8Script sceneScript, SceneShortInfo sceneShortInfo)
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
        public object LoadAndEvaluateCode(string moduleName) // "~system/EngineApi"
        {
            // Load just Scene Code
            if (moduleName == "~scene.js")
                return engine.Evaluate(sceneScript);

            string dirname = moduleName.Substring(0, 1);
            string filename = moduleName.Substring(1);

            // Load JavaScript wrapper in the Runtime
            V8Script moduleScript = moduleHub.GetModuleScript(filename);

            return engine.Evaluate(moduleScript);
        }
    }
}
