using Microsoft.ClearScript.V8;
using System;
using System.Collections.Generic;
using System.IO;

namespace SceneRuntime.ModuleHub
{
    public class SceneModuleHub : ISceneModuleHub
    {
        private readonly V8ScriptEngine engine;
        private readonly Dictionary<string, V8Script> jsNodulesCompiledScripts = new ();

        public SceneModuleHub(V8ScriptEngine engine)
        {
            this.engine = engine;
        }

        public void LoadAndCompileJsModule(string moduleName,
            ReadOnlySpan<byte> code)
        {
            V8Script script = engine.CompileScriptFromUtf8(code);
            string modulePath = $"system/{moduleName}";
            string extension = Path.GetExtension(moduleName);

            // "system/foo.js"
            jsNodulesCompiledScripts.Add(modulePath, script);

            // "system/foo"
            if (!string.IsNullOrEmpty(extension))
                jsNodulesCompiledScripts.Add(modulePath[..^extension.Length],
                    script);

            // We added a "system/" prefix to all our modules, but protobufjs,
            // a third party library, is not aware of that and is looking for
            // buffer and long in the wrong place.
            if (moduleName == "buffer.js")
                jsNodulesCompiledScripts.Add("buffer", script);
            else if (moduleName == "long.js")
                jsNodulesCompiledScripts.Add("long", script);
        }

        /// <summary>
        ///     Gets the compiled V8Script for the specified module name.
        /// </summary>
        /// <param name="moduleName">The name of the module to get the V8Script for.</param>
        /// <returns>The compiled V8Script for the specified module name.</returns>
        public V8Script ModuleScript(string moduleName)
        {
            if (jsNodulesCompiledScripts.TryGetValue(moduleName, out V8Script code))
                return code!;
            else
                throw new ArgumentException($"Module '{moduleName}' not found.");
        }
    }
}
