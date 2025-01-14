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
            string moduleDirectory = $"{Application.streamingAssetsPath}/Js/Modules";
            string[] modules = Directory.GetFiles(moduleDirectory, $"*{REQUIRED_EXTENSION}");

            return modules
                  .Select(i => Path.GetFileName(i))
                  .GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() =>
            GetEnumerator();
    }
}
