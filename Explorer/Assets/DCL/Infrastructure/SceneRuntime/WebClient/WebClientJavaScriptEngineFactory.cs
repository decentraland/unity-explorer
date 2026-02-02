using DCL.Diagnostics;

namespace SceneRuntime.WebClient
{
    public sealed class WebClientJavaScriptEngineFactory : IJavaScriptEngineFactory
    {
        public IJavaScriptEngine Create(SceneShortInfo sceneInfo) =>
            new WebClientJavaScriptEngine(sceneInfo.ToString());
    }
}
