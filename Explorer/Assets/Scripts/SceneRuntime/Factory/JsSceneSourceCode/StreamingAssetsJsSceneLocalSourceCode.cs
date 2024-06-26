using System.IO;
using UnityEngine;

namespace SceneRuntime.Factory.JsSceneSourceCode
{
    public class StreamingAssetsJsSceneLocalSourceCode : IJsSceneLocalSourceCode
    {
        private readonly string directoryPath;

        public StreamingAssetsJsSceneLocalSourceCode() : this(
            Path.Combine(Application.streamingAssetsPath!, "Js", "DebugScenes")
        ) { }

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
