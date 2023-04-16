using System;
using Microsoft.ClearScript.V8;

public class SceneApiFactory
{
    public static ISceneApi Load(string moduleName, SceneRuntime runtime)
    {
        switch (moduleName)
        {
            case "system/EngineApi":
                return new UnityEngineApi(runtime);
            default:
                throw new InvalidProgramException($"'{moduleName}' not found on Load");
        }
    }
}