using Newtonsoft.Json.Linq;
using System;
using UnityEngine;

namespace DCL.Mcp.Tools
{
    /// <summary>
    ///     Builders for the JSON fragments shared by tool outputs.
    /// </summary>
    public static class McpJson
    {
        public static JObject Vector(Vector3 value) =>
            new ()
            {
                ["x"] = Math.Round(value.x, 2),
                ["y"] = Math.Round(value.y, 2),
                ["z"] = Math.Round(value.z, 2),
            };

        public static JObject Parcel(Vector2Int value) =>
            new ()
            {
                ["x"] = value.x,
                ["y"] = value.y,
            };
    }
}
