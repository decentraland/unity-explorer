using Microsoft.ClearScript.V8;
using System;

namespace SceneRuntime.ModuleHub
{
    public interface ISceneModuleHub
    {
        void LoadAndCompileJsModule(string moduleName, ReadOnlySpan<byte> code);

        V8Script ModuleScript(string moduleName);
    }
}
