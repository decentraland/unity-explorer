using Newtonsoft.Json.Linq;
using System;
using UnityEngine;

namespace DCL.McpServer.Tools
{
    /// <summary>
    ///     Builders for the JSON fragments shared by tool outputs.
    /// </summary>
    public static class JObjectExtensions
    {
        public static JObject ToVector(this Vector3 value) =>
            new ()
            {
                ["x"] = Math.Round(value.x, 2),
                ["y"] = Math.Round(value.y, 2),
                ["z"] = Math.Round(value.z, 2),
            };

        public static JObject ToParcel(this Vector2Int value) =>
            new ()
            {
                ["x"] = value.x,
                ["y"] = value.y,
            };
    }
}
