using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace SceneRuntime.Factory
{
    public class JsModulesNameList : IEnumerable<string>
    {
        private const string REQUIRED_EXTENSION = ".js";

        // Hardcoded list of JS modules for WebGL builds where Directory.GetFiles is not available
        // This list must be kept in sync with the files in StreamingAssets/Js/Modules/
        private static readonly string[] WEBGL_MODULE_NAMES = new[]
        {
            "buffer.js",
            "CommsApi.js",
            "CommunicationsController.js",
            "EngineApi.js",
            "EnvironmentApi.js",
            "EthereumController.js",
            "FetchApi.js",
            "long.js",
            "Players.js",
            "PortableExperiences.js",
            "RestrictedActions.js",
            "Runtime.js",
            "Scene.js",
            "SignedFetch.js",
            "Testing.js",
            "UserActionModule.js",
            "UserIdentity.js",
            "WebSocketApi.js"
        };

        public IEnumerator<string> GetEnumerator()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // In WebGL, we can't use Directory.GetFiles, so use the hardcoded list
            return ((IEnumerable<string>)WEBGL_MODULE_NAMES).GetEnumerator();
#else
            return Directory
                  .GetFiles(
                       Path.Join(Application.streamingAssetsPath, "/Js/Modules/")!
                   )
                  .Select(Path.GetFileName)
                  .Where(e => Path.GetExtension(e) == REQUIRED_EXTENSION)
                  .GetEnumerator();
#endif
        }

        IEnumerator IEnumerable.GetEnumerator() =>
            GetEnumerator();
    }
}
