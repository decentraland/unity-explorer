using System;
using System.Collections.Generic;
using System.IO;

namespace SceneRuntime.ModuleHub
{
    public class SceneModuleHub : ISceneModuleHub
    {
        private readonly IJavaScriptEngine engine;
        private readonly Dictionary<string, ICompiledScript> jsNodulesCompiledScripts = new ();

        public SceneModuleHub(IJavaScriptEngine engine)
        {
            this.engine = engine;
        }

        public void LoadAndCompileJsModules(IReadOnlyDictionary<string, string> sources)
        {
            foreach (KeyValuePair<string, string> source in sources)
            {
                ICompiledScript script = engine.Compile(source.Value);
                var moduleName = $"system/{source.Key}";
                string extension = Path.GetExtension(moduleName);

                // "system/foo.js"
                jsNodulesCompiledScripts.Add(moduleName, script);

                // "system/foo"
                if (!string.IsNullOrEmpty(extension))
                    jsNodulesCompiledScripts.Add(moduleName[..^extension.Length], script);

                // We added a "system/" prefix to all our modules, but protobufjs, a third party
                // library, is not aware of that and is looking for buffer and long in the wrong place.
                if (source.Key == "buffer.js")
                    jsNodulesCompiledScripts.Add("buffer", script);
                else if (source.Key == "long.js")
                    jsNodulesCompiledScripts.Add("long", script);
            }
        }

        /// <summary>
        ///     Gets the compiled script for the specified module name.
        /// </summary>
        /// <param name="moduleName">The name of the module to get the script for.</param>
        /// <returns>The compiled script for the specified module name.</returns>
        public ICompiledScript ModuleScript(string moduleName)
        {
            if (jsNodulesCompiledScripts.TryGetValue(moduleName, out ICompiledScript code))
                return code!;
            else
                throw new ArgumentException($"Module '{moduleName}' not found.");
        }
    }
}
