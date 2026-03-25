using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;

namespace DCL.WebGL
{
    /// <summary>
    /// Keeps WEBGL_ACTIVE in sync with EDITOR_DEBUG_WEBGL for the WebGL platform.
    /// WEBGL_ACTIVE means: "this code only runs on WebGL — shown in editor to surface
    /// compile errors when EDITOR_DEBUG_WEBGL is defined."
    /// Removing EDITOR_DEBUG_WEBGL automatically removes WEBGL_ACTIVE too.
    /// </summary>
    [InitializeOnLoad]
    public static class WebGLActiveDefineApplicator
    {
        private const string WEBGL_ACTIVE = "WEBGL_ACTIVE";

        static WebGLActiveDefineApplicator()
        {
             Sync(EditorUserBuildSettings.activeBuildTarget);
        }

        private static void Sync(BuildTarget target)
        {

            NamedBuildTarget namedTarget = NamedBuildTarget.FromBuildTargetGroup(
                BuildPipeline.GetBuildTargetGroup(target));

            PlayerSettings.GetScriptingDefineSymbols(namedTarget, out string[] defines);
            var list = new List<string>(defines);

            bool alreadyHas = list.Contains(WEBGL_ACTIVE);

#if UNITY_WEBGL && (!UNITY_EDITOR || EDITOR_DEBUG_WEBGL)
            if (alreadyHas) return;
            list.Add(WEBGL_ACTIVE);

#else
            if (alreadyHas)
                list.Remove(WEBGL_ACTIVE);
            else
                return;
#endif
            PlayerSettings.SetScriptingDefineSymbols(namedTarget, list.ToArray());
        }
    }
}
