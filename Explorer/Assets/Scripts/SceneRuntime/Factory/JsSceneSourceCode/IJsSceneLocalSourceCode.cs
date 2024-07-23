using DCL.Diagnostics;
using UnityEngine;

namespace SceneRuntime.Factory.JsSceneSourceCode
{
    public interface IJsSceneLocalSourceCode
    {
        string? CodeForScene(Vector2Int coordinates);

        public class Null : IJsSceneLocalSourceCode
        {
            public string? CodeForScene(Vector2Int coordinates) =>
                null;
        }

        public class Default : IJsSceneLocalSourceCode
        {
            private readonly IJsSceneLocalSourceCode origin;

            public Default()
            {
                origin = Application.isEditor
                    ? new LogJsSceneLocalSourceCode(
                        new StreamingAssetsJsSceneLocalSourceCode(),
                        ReportHub.WithReport(ReportCategory.SCENE_FACTORY).Log
                    )
                    : new Null();
            }

            public string? CodeForScene(Vector2Int coordinates) =>
                origin.CodeForScene(coordinates);
        }
    }
}
