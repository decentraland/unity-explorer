using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System.Collections.Generic;

namespace DCL.McpServer.Tests
{
    /// <summary>
    ///     Guards against output-schema ↔ structuredContent drift: a tool declares its OutputSchema by hand and
    ///     builds the matching payload by hand, so a field added to one and forgotten in the other lets the schema
    ///     silently misdescribe the payload to the agent. Asserting the two carry the same property names
    ///     (recursively, into nested objects the payload populates) turns that drift into a failing test.
    /// </summary>
    internal static class McpSchemaAssert
    {
        public static void KeysMatch(JObject schema, JObject payload)
        {
            var properties = schema["properties"] as JObject ?? new JObject();

            CollectionAssert.AreEquivalent(NamesOf(properties), NamesOf(payload),
                $"Output schema and payload disagree on the keys of '{PathOf(schema)}'.");

            foreach (JProperty property in properties.Properties())
            {
                if (DeclaresObject(property.Value) && payload[property.Name] is JObject nested)
                    KeysMatch((JObject)property.Value, nested);
                else if (property.Value["items"] is JObject items && DeclaresObject(items) && payload[property.Name] is JArray { Count: > 0 } array && array[0] is JObject firstItem)
                    KeysMatch(items, firstItem);
            }
        }

        private static List<string> NamesOf(JObject obj)
        {
            var names = new List<string>();

            foreach (JProperty property in obj.Properties())
                names.Add(property.Name);

            return names;
        }

        // A property models an object both as type "object" and as a nullable ["object", "null"] union.
        private static bool DeclaresObject(JToken schema)
        {
            JToken? type = schema["type"];

            if (type is JArray union)
            {
                foreach (JToken member in union)
                    if (member.Value<string>() == "object")
                        return true;

                return false;
            }

            return type?.Value<string>() == "object";
        }

        private static string PathOf(JObject schema) =>
            string.IsNullOrEmpty(schema.Path) ? "<root>" : schema.Path;
    }
}
