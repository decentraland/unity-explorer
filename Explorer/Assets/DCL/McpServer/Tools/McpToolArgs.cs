using Newtonsoft.Json.Linq;

namespace DCL.McpServer.Tools
{
    /// <summary>
    ///     Typed accessors over the tools/call arguments object shared by all tool implementations.
    /// </summary>
    public static class McpToolArgs
    {
        public static bool GetBool(this JObject arguments, string name, bool defaultValue) =>
            arguments[name]?.Type == JTokenType.Boolean ? arguments[name]!.Value<bool>() : defaultValue;

        public static int GetInt(this JObject arguments, string name, int defaultValue) =>
            IsNumber(arguments[name]) ? arguments[name]!.Value<int>() : defaultValue;

        public static long GetLong(this JObject arguments, string name, long defaultValue) =>
            IsNumber(arguments[name]) ? arguments[name]!.Value<long>() : defaultValue;

        public static float GetFloat(this JObject arguments, string name, float defaultValue) =>
            IsNumber(arguments[name]) ? arguments[name]!.Value<float>() : defaultValue;

        public static string GetString(this JObject arguments, string name, string defaultValue) =>
            arguments[name]?.Type == JTokenType.String ? arguments[name]!.Value<string>()! : defaultValue;

        public static bool TryGetFloat(this JObject arguments, string name, out float value)
        {
            if (IsNumber(arguments[name]))
            {
                value = arguments[name]!.Value<float>();
                return true;
            }

            value = 0f;
            return false;
        }

        public static bool TryGetInt(this JObject arguments, string name, out int value)
        {
            if (IsNumber(arguments[name]))
            {
                value = arguments[name]!.Value<int>();
                return true;
            }

            value = 0;
            return false;
        }

        private static bool IsNumber(JToken? token) =>
            token?.Type is JTokenType.Integer or JTokenType.Float;
    }
}
