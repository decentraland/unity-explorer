using Microsoft.ClearScript.V8;
using System.Collections.Generic;

namespace SceneRuntime.ModuleHub
{
    public interface ISceneModuleHub
    {
        void LoadAndCompileJsModules(IReadOnlyDictionary<string, string> sources);

        V8Script ModuleScript(string moduleName);
    }
}
