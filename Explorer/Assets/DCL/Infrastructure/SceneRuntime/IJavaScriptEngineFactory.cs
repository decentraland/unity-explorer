using DCL.Diagnostics;

namespace SceneRuntime
{
    public interface IJavaScriptEngineFactory
    {
        IJavaScriptEngine Create(SceneShortInfo sceneInfo);
    }
}
