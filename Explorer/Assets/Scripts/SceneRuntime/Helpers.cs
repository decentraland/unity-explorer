// TODO: This helpers can be temporarly

using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

public class Helpers
{

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

}
