using System;
using UnityEngine;

namespace SceneRuntime.Factory.JsSceneSourceCode
{
    public class LogJsSceneLocalSourceCode : IJsSceneLocalSourceCode
    {
        private readonly IJsSceneLocalSourceCode origin;
        private readonly Action<string> log;

        public LogJsSceneLocalSourceCode(IJsSceneLocalSourceCode origin, Action<string> log)
        {
            this.origin = origin;
            this.log = log;
        }

        public string? CodeForScene(Vector2Int coordinates)
        {
            string? result = origin.CodeForScene(coordinates);
            log($"Code for scene {coordinates} is {(result != null ? "found" : "not found")}");
            return result;
        }
    }
}
