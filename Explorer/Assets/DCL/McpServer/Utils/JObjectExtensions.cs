using DCL.McpServer.Core;
using Newtonsoft.Json.Linq;
using System;
using UnityEngine;

namespace DCL.McpServer.Utils
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

        /// <summary>Output-schema counterpart of <see cref="ToVector" /> — an { x, y, z } object of numbers.</summary>
        public static McpJsonSchema VectorSchema() =>
            McpJsonSchema.Object()
                          .Number("x")
                          .Number("y")
                          .Number("z");

        /// <summary>Output-schema counterpart of <see cref="ToParcel" /> — an { x, y } object of integers.</summary>
        public static McpJsonSchema ParcelSchema() =>
            McpJsonSchema.Object()
                          .Integer("x")
                          .Integer("y");

        public static bool GetBool(this JObject arguments, string name, bool defaultValue) =>
            arguments[name]?.Type == JTokenType.Boolean ? arguments[name]!.Value<bool>() : defaultValue;

        public static int GetInt(this JObject arguments, string name, int defaultValue) =>
            arguments[name].IsNumber() ? arguments[name]!.Value<int>() : defaultValue;

        public static long GetLong(this JObject arguments, string name, long defaultValue) =>
            arguments[name].IsNumber() ? arguments[name]!.Value<long>() : defaultValue;

        public static float GetFloat(this JObject arguments, string name, float defaultValue) =>
            arguments[name].IsNumber() ? arguments[name]!.Value<float>() : defaultValue;

        public static string GetString(this JObject arguments, string name, string defaultValue) =>
            arguments[name]?.Type == JTokenType.String ? arguments[name]!.Value<string>()! : defaultValue;

        /// <summary>
        ///     Reads an enum argument sent as its wire name (see <see cref="McpWireEnum{T}" />). False when the
        ///     argument is missing, not a string, not a member, or outside <paramref name="allowed" />.
        /// </summary>
        public static bool TryGetEnum<T>(this JObject arguments, string name, out T value, T[]? allowed = null) where T : struct, Enum
        {
            if (arguments[name]?.Type == JTokenType.String
                && McpWireEnum<T>.TryParse(arguments[name]!.Value<string>()!, out value)
                && (allowed == null || Array.IndexOf(allowed, value) >= 0))
                return true;

            value = default(T);
            return false;
        }

        /// <summary>Same as <see cref="TryGetEnum{T}(JObject,string,out T,T[])" />, but a missing argument yields <paramref name="defaultValue" /> instead of failing.</summary>
        public static bool TryGetEnum<T>(this JObject arguments, string name, T defaultValue, out T value, T[]? allowed = null) where T : struct, Enum
        {
            if (arguments[name] == null)
            {
                value = defaultValue;
                return true;
            }

            return arguments.TryGetEnum(name, out value, allowed);
        }

        public static bool TryGetFloat(this JObject arguments, string name, out float value)
        {
            if (arguments[name].IsNumber())
            {
                value = arguments[name]!.Value<float>();
                return true;
            }

            value = 0f;
            return false;
        }

        public static bool TryGetInt(this JObject arguments, string name, out int value)
        {
            if (arguments[name].IsNumber())
            {
                value = arguments[name]!.Value<int>();
                return true;
            }

            value = 0;
            return false;
        }

        private static bool IsNumber(this JToken? token) =>
            token?.Type is JTokenType.Integer or JTokenType.Float;
    }
}
