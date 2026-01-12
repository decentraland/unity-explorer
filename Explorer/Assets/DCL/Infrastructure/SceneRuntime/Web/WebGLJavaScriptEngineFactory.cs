using DCL.Diagnostics;

namespace SceneRuntime.Web
{
    public sealed class WebGLJavaScriptEngineFactory : IJavaScriptEngineFactory
    {
        public IJavaScriptEngine Create(SceneShortInfo sceneInfo) =>
            new WebGLJavaScriptEngine(sceneInfo.ToString());
    }
}
