using Newtonsoft.Json.Linq;
using System;

namespace DCL.McpServer.Core
{
    /// <summary>
    ///     Fluent builder for a tool's input or output JSON Schema. Declaring each field through a typed method keeps
    ///     the schema typo-safe — a mistyped type name can't slip through the way it could in a raw JSON string — and
    ///     produces the same { type: object, properties, required } shape a tools/list entry expects. Nested objects,
    ///     integer arrays and nullable fields extend the same style to the richer shapes an outputSchema needs.
    /// </summary>
    public sealed class McpJsonSchema
    {
        private readonly JObject properties = new ();
        private readonly JArray requiredNames = new ();

        private McpJsonSchema() { }

        /// <summary>Starts a schema describing an object; chain the field methods and finish with <see cref="Build" />.</summary>
        public static McpJsonSchema Object() =>
            new ();

        public McpJsonSchema String(string name, string? description = null, string[]? enumValues = null, bool isRequired = false, bool nullable = false) =>
            Property(name, "string", description, enumValues, isRequired, nullable);

        /// <summary>
        ///     Adds a string field constrained to the wire names of <typeparamref name="T" />'s members (see
        ///     <see cref="McpWireEnum{T}" />), so the schema and the parsing of the argument share the enum as
        ///     the single source of truth. <paramref name="allowed" /> narrows the choices to a subset of members.
        /// </summary>
        public McpJsonSchema Enum<T>(string name, string? description = null, T[]? allowed = null, bool isRequired = false) where T : struct, Enum =>
            Property(name, "string", description, allowed == null ? McpWireEnum<T>.WIRE_NAMES : McpWireEnum<T>.WireNamesOf(allowed), isRequired, false);

        public McpJsonSchema Number(string name, string? description = null, bool isRequired = false, bool nullable = false) =>
            Property(name, "number", description, null, isRequired, nullable);

        public McpJsonSchema Integer(string name, string? description = null, bool isRequired = false, bool nullable = false) =>
            Property(name, "integer", description, null, isRequired, nullable);

        public McpJsonSchema Boolean(string name, string? description = null, bool isRequired = false, bool nullable = false) =>
            Property(name, "boolean", description, null, isRequired, nullable);

        /// <summary>
        ///     Adds a nested object field described by its own <paramref name="schema" /> builder. A
        ///     <paramref name="nullable" /> field admits null in place of the object (JSON Schema "type": ["object", "null"]).
        /// </summary>
        public McpJsonSchema Object(string name, McpJsonSchema schema, string? description = null, bool isRequired = false, bool nullable = false)
        {
            JObject field = schema.Build();

            if (nullable)
                field["type"] = TypeToken("object", true);

            if (description != null)
                field["description"] = description;

            return AddField(name, field, isRequired);
        }

        /// <summary>Adds an array field whose items are all integers.</summary>
        public McpJsonSchema IntegerArray(string name, string? description = null, bool isRequired = false)
        {
            var field = new JObject
            {
                ["type"] = "array",
                ["items"] = new JObject { ["type"] = "integer" },
            };

            if (description != null)
                field["description"] = description;

            return AddField(name, field, isRequired);
        }

        /// <summary>Adds an array field whose items are all objects of the shape <paramref name="items" /> describes.</summary>
        public McpJsonSchema ObjectArray(string name, McpJsonSchema items, string? description = null, bool isRequired = false)
        {
            var field = new JObject
            {
                ["type"] = "array",
                ["items"] = items.Build(),
            };

            if (description != null)
                field["description"] = description;

            return AddField(name, field, isRequired);
        }

        /// <summary>Materializes the accumulated fields into the JSON Schema object.</summary>
        public JObject Build()
        {
            var schema = new JObject
            {
                ["type"] = "object",
                ["properties"] = properties,
            };

            if (requiredNames.Count > 0)
                schema["required"] = requiredNames;

            return schema;
        }

        private McpJsonSchema Property(string name, string type, string? description, string[]? enumValues, bool isRequired, bool nullable)
        {
            var field = new JObject { ["type"] = TypeToken(type, nullable) };

            if (description != null)
                field["description"] = description;

            if (enumValues != null)
            {
                var values = new JArray();

                foreach (string value in enumValues)
                    values.Add(value);

                field["enum"] = values;
            }

            return AddField(name, field, isRequired);
        }

        private static JToken TypeToken(string type, bool nullable) =>
            nullable ? new JArray { type, "null" } : type;

        private McpJsonSchema AddField(string name, JObject field, bool isRequired)
        {
            properties[name] = field;

            if (isRequired)
                requiredNames.Add(name);

            return this;
        }
    }
}
