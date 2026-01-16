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

        // TODO must be rewritten to load all modules by the HTTP path
        public IEnumerator<string> GetEnumerator()
        {
#if UNITY_WEBGL
            yield break; 
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
