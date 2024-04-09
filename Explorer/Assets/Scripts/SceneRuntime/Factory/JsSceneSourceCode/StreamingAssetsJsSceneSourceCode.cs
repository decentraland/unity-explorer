using System.IO;
using UnityEngine;

namespace SceneRuntime.Factory.JsSceneSourceCode
{
    public class StreamingAssetsJsSceneSourceCode : IJsSceneSourceCode
    {
        private readonly string directoryPath;

        public StreamingAssetsJsSceneSourceCode() : this(
            Path.Combine(Application.streamingAssetsPath!, "Js", "DebugScenes")
        ) { }

        public StreamingAssetsJsSceneSourceCode(string directoryPath)
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
