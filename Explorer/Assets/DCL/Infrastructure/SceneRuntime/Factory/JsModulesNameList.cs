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

        public IEnumerator<string> GetEnumerator()
        {
            return Directory
                  .GetFiles(
                       Path.Join(Application.streamingAssetsPath, "/Js/Modules/")!
                   )
                  .Select(Path.GetFileName)
                  .Where(e => Path.GetExtension(e) == REQUIRED_EXTENSION)
                  .GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() =>
            GetEnumerator();
    }
}
