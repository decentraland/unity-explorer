using System;
using UnityEngine;

namespace SceneRuntime.Factory.JsSceneSourceCode
{
    public class LogJsSceneSourceCode : IJsSceneSourceCode
    {
        private readonly IJsSceneSourceCode origin;
        private readonly Action<string> log;

        public LogJsSceneSourceCode(IJsSceneSourceCode origin, Action<string> log)
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
