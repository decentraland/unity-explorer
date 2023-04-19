using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.ClearScript.V8;
using Unity.VisualScripting;
using UnityEngine;

public class SceneModuleLoader
{
    private readonly Dictionary<string, V8Script> jsNodulesCompiledScripts = new();
    
    public void LoadAndCompileJsModules(V8ScriptEngine engine)
    {
        // Get an array of file paths for all JavaScript module files in the project
        var sources = Helpers.GetModulesSources();
        
        foreach (var source in sources)
        {
            // Wrap the source code in a CommonJS module wrapper
            var commonJsModule = Helpers.ModuleWrapperCommonJs(source.text);

            // Compile the module using the V8ScriptEngine
            V8Script script = engine.Compile(commonJsModule);

            // Add the compiled script to a dictionary with the module name as the key
            jsNodulesCompiledScripts.Add("system/" + source.name, script);
        }
    }

    /// <summary>
    /// Gets the compiled V8Script for the specified module name.
    /// </summary>
    /// <param name="moduleName">The name of the module to get the V8Script for.</param>
    /// <returns>The compiled V8Script for the specified module name.</returns>
    public V8Script GetModuleScript(string moduleName)
    {
        // Check if the module name is in the dictionary of compiled scripts
        if (jsNodulesCompiledScripts.TryGetValue(moduleName, out var code))
        {
            return code;
        }
    
        // If not, try appending ".js" to the module name
        var moduleNameWithJs = moduleName + ".js";
        if (jsNodulesCompiledScripts.TryGetValue(moduleNameWithJs, out code))
        {
            return code;
        }

        // If we don't find a match, throw an exception
        throw new ArgumentException($"Module '{moduleName}' not found.");
    }
}