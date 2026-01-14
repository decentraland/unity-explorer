using System.Collections.Generic;

namespace SceneRuntime.ModuleHub
{
    public interface ISceneModuleHub
    {
        void LoadAndCompileJsModules(IReadOnlyDictionary<string, string> sources);

        ICompiledScript ModuleScript(string moduleName);
    }
}
