using UnityEngine;

namespace SceneRuntime.Factory.JsSceneSourceCode
{
    public interface IJsSceneSourceCode
    {
        string? CodeForScene(Vector2Int coordinates);

        public class Null : IJsSceneSourceCode
        {
            public string? CodeForScene(Vector2Int coordinates) =>
                null;
        }
    }
}
