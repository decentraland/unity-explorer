using DCL.McpServer.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Text;
using UnityEngine;

namespace DCL.McpServer.Utils
{
    /// <summary>
    ///     Builders for the JSON fragments shared by tool outputs.
    /// </summary>
    public static class JObjectExtensions
    {
        /// <summary>Cap on how much of an unusable argument an error echoes back.</summary>
        private const int MAX_ECHOED_ARGUMENT_LENGTH = 60;

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

        /// <summary>
        ///     A clause naming every one of <paramref name="names" /> that arrived but not as a number, to append
        ///     to a tool's own "required argument" error. A caller that sends a coordinate as a string otherwise
        ///     gets an error naming a cause that is not true ("provide a full x/y/z" when all three were sent),
        ///     which costs a live run several calls to attribute. Empty when there is nothing to name: an argument
        ///     that is simply absent is already covered by the tool's own message.
        /// </summary>
        public static string NonNumericHint(this JObject arguments, params string[] names)
        {
            StringBuilder? hint = null;

            foreach (string name in names)
            {
                JToken? token = arguments[name];

                if (token == null || token.IsNumber())
                    continue;

                hint ??= new StringBuilder(" (");

                if (hint.Length > 2)
                    hint.Append("; ");

                hint.Append(name).Append(" arrived as ").Append(Describe(token)).Append(", not a number");
            }

            return hint == null ? string.Empty : hint.Append(')').ToString();
        }

        /// <summary>What a token is, plus what it held — truncated, because a caller can pass anything.</summary>
        private static string Describe(JToken token)
        {
            if (token.Type == JTokenType.Null)
                return "null";

            string text = token.ToString(Formatting.None);

            if (text.Length > MAX_ECHOED_ARGUMENT_LENGTH)
                text = text.Substring(0, MAX_ECHOED_ARGUMENT_LENGTH) + "…";

            return $"{token.Type.ToString().ToLowerInvariant()} {text}";
        }

        private static bool IsNumber(this JToken? token) =>
            token?.Type is JTokenType.Integer or JTokenType.Float;
    }
}
