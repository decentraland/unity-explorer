#if UNITY_WEBGL && (!UNITY_EDITOR || EDITOR_DEBUG_WEBGL)
using DCL.Diagnostics;

namespace SceneRuntime.WebClient
{
    /// <summary>
    ///     Factory that creates <see cref="WebClientJavaScriptEngine" /> instances for WebGL scene runtimes.
    ///     Each call to <see cref="Create" /> produces a new engine whose JS context is keyed by the scene's short info string.
    /// </summary>
    public sealed class WebClientJavaScriptEngineFactory : IJavaScriptEngineFactory
    {
        public IJavaScriptEngine Create(SceneShortInfo sceneInfo) =>
            new WebClientJavaScriptEngine(sceneInfo.ToString());
    }
}
#endif
