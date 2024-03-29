using Microsoft.ClearScript.V8;
using System;
using System.Collections.Generic;

namespace SceneRuntime
{
    public class SceneModuleLoader
    {
        private readonly Dictionary<string, V8Script> jsNodulesCompiledScripts = new ();

        public void LoadAndCompileJsModules(V8ScriptEngine engine, IReadOnlyDictionary<string, string> sources)
        {
            foreach (string filename in sources.Keys)
            {
                // Compile the module using the V8ScriptEngine
                V8Script script = engine.Compile(sources[filename]);

                // Add the compiled script to a dictionary with the module name as the key
                jsNodulesCompiledScripts.Add($"system/{filename}", script);
            }
        }

        /// <summary>
        ///     Gets the compiled V8Script for the specified module name.
        /// </summary>
        /// <param name="moduleName">The name of the module to get the V8Script for.</param>
        /// <returns>The compiled V8Script for the specified module name.</returns>
        public V8Script GetModuleScript(string moduleName)
        {
            // Check if the module name is in the dictionary of compiled scripts
            if (jsNodulesCompiledScripts.TryGetValue(moduleName, out V8Script code)) { return code; }

            // If not, try appending ".js" to the module name
            string moduleNameWithJs = moduleName + ".js";

            if (jsNodulesCompiledScripts.TryGetValue(moduleNameWithJs, out code)) { return code; }

            // If we don't find a match, throw an exception
            throw new ArgumentException($"Module '{moduleName}' not found.");
        }
    }
}
