using DCL.Diagnostics;
using SceneRunner.Scene;

namespace SceneRuntime
{
    public interface IJavaScriptEngineFactory
    {
        IJavaScriptEngine Create(SceneShortInfo sceneInfo);
    }
}
