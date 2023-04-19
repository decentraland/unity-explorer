// TODO: This helpers can be temporarly

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

public class Helpers
{
    // SourceCode Cache
    private static readonly Dictionary<string, string> sourceCodeCache = new();

    // Wrapper https://nodejs.org/api/modules.html#the-module-wrapper
    public static string ModuleWrapperCommonJs(string source)
    {
        // create a wrapper for the script
        source = Regex.Replace(source, @"^#!.*?\n", "");
        string head = "(function (exports, require, module, __filename, __dirname) { (function (exports, require, module, __filename, __dirname) {";
        string foot = "\n}).call(this, exports, require, module, __filename, __dirname); })";
        source = $"{head}{source}{foot}";
        return source;
    }
    
    public static string LoadJavaScriptSourceCode(string fileName)
    {
        if (sourceCodeCache.TryGetValue(fileName, out var value))
        {
            return value;
        }
        
        var sourceCodeAsset = Resources.Load<TextAsset>(fileName);

        if (sourceCodeAsset == null)
            throw new InvalidProgramException($"Source Asset '{fileName}' no exists");

        sourceCodeCache.Add(fileName, sourceCodeAsset.text);
        return sourceCodeAsset.text;
    }

    /// <summary>
    /// Gets an array of the modules source code for all JavaScript module files in the project.
    /// </summary>
    /// <returns>An array of source codes for all JavaScript module files in the project.</returns>
    public static TextAsset[] GetModulesSources()
    {
        return Resources.LoadAll<TextAsset>("Js/Modules/"); // Return an array of source codes, with the paths being relative to the root folder
    }

}