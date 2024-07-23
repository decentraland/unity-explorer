using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace SceneRuntime.Factory.JsSceneSourceCode
{
    public class StreamingAssetsJsSceneLocalSourceCode : IJsSceneLocalSourceCode
    {
        public static readonly string DIRECTORY_PATH = Path.Combine(Application.streamingAssetsPath!, "Js", "DebugScenes");

        public static readonly Regex SCENE_NAME_PATTERN = new (@"^(?<x>-?\d+), (?<y>-?\d+).js$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly string directoryPath;

        public StreamingAssetsJsSceneLocalSourceCode() : this(DIRECTORY_PATH) { }

        public StreamingAssetsJsSceneLocalSourceCode(string directoryPath)
        {
            this.directoryPath = directoryPath;
        }

        public string? CodeForScene(Vector2Int coordinates)
        {
            string filePath = Path.Combine(directoryPath, $"{coordinates.x}, {coordinates.y}.js");

            return File.Exists(filePath)
                ? File.ReadAllText(filePath)
                : null;
        }
    }
}
