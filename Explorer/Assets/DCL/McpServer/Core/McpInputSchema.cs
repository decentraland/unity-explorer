using Newtonsoft.Json.Linq;

namespace DCL.McpServer.Core
{
    /// <summary>
    ///     Fluent builder for a tool's input JSON Schema. Declaring each field through a typed method keeps the
    ///     schema typo-safe — a mistyped type name can't slip through the way it could in a raw JSON string — and
    ///     produces the same { type: object, properties, required } shape a tools/list entry expects.
    /// </summary>
    public sealed class McpInputSchema
    {
        private readonly JObject properties = new ();
        private readonly JArray required = new ();

        private McpInputSchema() { }

        /// <summary>Starts a schema describing an object; chain the field methods and finish with <see cref="Build" />.</summary>
        public static McpInputSchema Object() =>
            new ();

        public McpInputSchema String(string name, string? description = null, string[]? enumValues = null, bool required = false) =>
            Property(name, "string", description, enumValues, required);

        public McpInputSchema Number(string name, string? description = null, bool required = false) =>
            Property(name, "number", description, null, required);

        public McpInputSchema Integer(string name, string? description = null, bool required = false) =>
            Property(name, "integer", description, null, required);

        public McpInputSchema Boolean(string name, string? description = null, bool required = false) =>
            Property(name, "boolean", description, null, required);

        /// <summary>Materializes the accumulated fields into the JSON Schema object.</summary>
        public JObject Build()
        {
            var schema = new JObject
            {
                ["type"] = "object",
                ["properties"] = properties,
            };

            if (required.Count > 0)
                schema["required"] = required;

            return schema;
        }

        private McpInputSchema Property(string name, string type, string? description, string[]? enumValues, bool isRequired)
        {
            var field = new JObject { ["type"] = type };

            if (description != null)
                field["description"] = description;

            if (enumValues != null)
            {
                var values = new JArray();

                foreach (string value in enumValues)
                    values.Add(value);

                field["enum"] = values;
            }

            properties[name] = field;

            if (isRequired)
                required.Add(name);

            return this;
        }
    }
}
