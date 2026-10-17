using System;
using UnityEngine;
using DCL.Diagnostics;

namespace SceneRuntime.Factory.JsSceneSourceCode
{
    public class LogJsSceneLocalSourceCode : IJsSceneLocalSourceCode
    {
        private readonly IJsSceneLocalSourceCode origin;

        public LogJsSceneLocalSourceCode(IJsSceneLocalSourceCode origin)
        {
            this.origin = origin;
        }

        public string? CodeForScene(Vector2Int coordinates)
        {
            string? result = origin.CodeForScene(coordinates);
            ReportHub
               .WithReport(ReportCategory.SCENE_LOADING)
               .Log($"Code for scene {coordinates} is {(result != null ? "found" : "not found")}");
            return result;
        }
    }
}
