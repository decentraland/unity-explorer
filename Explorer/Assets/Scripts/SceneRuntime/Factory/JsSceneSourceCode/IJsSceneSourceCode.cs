using DCL.Diagnostics;
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

        public class Default : IJsSceneSourceCode
        {
            private readonly IJsSceneSourceCode origin;

            public Default()
            {
                origin = Application.isEditor
                    ? new LogJsSceneSourceCode(
                        new StreamingAssetsJsSceneSourceCode(),
                        ReportHub.WithReport(ReportCategory.SCENE_FACTORY).Log
                    )
                    : new Null();
            }

            public string? CodeForScene(Vector2Int coordinates) =>
                origin.CodeForScene(coordinates);
        }
    }
}
