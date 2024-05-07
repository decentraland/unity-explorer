#if UNITY_CLOUD_BUILD
using JetBrains.Annotations;
using System;
using UnityEngine;

namespace Editor
{
    public static class CloudBuild
    {
        [UsedImplicitly]
        public static void PreExport()
        {
            Debug.Log("PreExport");
            string testEnvGit = Environment.GetEnvironmentVariable("TEST_ENV_GIT");
            Debug.Log(testEnvGit);
        }

        [UsedImplicitly]
        public static void PostExport()
        {
            Debug.Log("PostExport");
            string testEnvGit = Environment.GetEnvironmentVariable("TEST_ENV_GIT");
            Debug.Log(testEnvGit);
        }
    }
}
#endif
