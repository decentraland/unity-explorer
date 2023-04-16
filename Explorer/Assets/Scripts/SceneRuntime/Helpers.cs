// TODO: This helpers can be temporarly
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.ClearScript.V8;
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
        
        var reader = new StreamReader(Application.dataPath + "/Scripts/SceneRuntime/Js/" + fileName);
        var sourceCode = reader.ReadToEnd();
        reader.Close();
        return sourceCode;
    }

    public static string LoadSceneSourceCode(string fileName)
    {
        return LoadJavaScriptSourceCode("Scenes/" + fileName + "/game_js");
    }

    public static string LoadModuleSourceCode(string fileName)
    {
        return LoadJavaScriptSourceCode("Modules/" + fileName);
    }
    
    /// <summary>
    /// Gets an array of file paths for all JavaScript module files in the project.
    /// </summary>
    /// <returns>An array of file paths for all JavaScript module files in the project.</returns>
    public static string[] GetModulesFiles()
    {
        var rootFolder = Application.dataPath + "/Scripts/SceneRuntime/Js/Modules/";
        var searchPattern = "*.js"; // The search pattern to use, will only search for files ending in .js
        var searchOption = SearchOption.AllDirectories; // The search option, will search all subdirectories as well

        var files = Directory.GetFiles(rootFolder, searchPattern, searchOption); // Get all the files that match the search pattern

        for (var i = 0; i < files.Length; i++) {
            files[i] = Path.GetRelativePath(rootFolder, files[i]).Replace("\\", "/"); // Convert the file paths to be relative to the root folder
        }

        return files; // Return an array of file paths, with the paths being relative to the root folder
    }

}