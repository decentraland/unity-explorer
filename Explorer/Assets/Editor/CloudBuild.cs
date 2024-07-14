
using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
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
        }

        [UsedImplicitly]
        public static void PostExport()
        {
            Debug.Log("~~ PostExport ~~");
        }
    }
}
