
using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Editor
{
    public static class CloudBuild
    {
        [PublicAPI]
        public static Dictionary<string, object> Parameters { get; private set; }

        [UsedImplicitly]
        public static void PreExport()
        {
            Debug.Log("~~ PreExport ~~");

            // Get all environment variables
            var environmentVariables = Environment.GetEnvironmentVariables();
            Parameters = environmentVariables.Cast<DictionaryEntry>().ToDictionary(x => x.Key.ToString(), x => x.Value);

            // E.g. access like:
            Debug.Log(Parameters["TEST_VALUE"]);
        }

        [UsedImplicitly]
        public static void PostExport()
        {
            Debug.Log("~~ PostExport ~~");
        }
    }
}
