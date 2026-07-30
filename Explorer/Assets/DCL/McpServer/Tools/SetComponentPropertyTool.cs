#if MCP_TEST_AUTOMATION
using Cysharp.Threading.Tasks;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using Newtonsoft.Json.Linq;
using System.Threading;
using UnityEngine;

namespace DCL.McpServer.Tools
{
    /// <summary>
    ///     Writes one property of one component of a resolved UI element. Which member is unrestricted by design;
    ///     what keeps that acceptable is the reach of the tool itself — a UI element of the running client — plus the
    ///     two build gates <see cref="CallStaticMethodTool" /> documents.
    /// </summary>
    public class SetComponentPropertyTool : McpTool
    {
        public override string Name => "set_component_property";

        public override string Description =>
            "Write a property or field on a component of a client UI element — the counterpart of "
            + "get_component_property, for forcing a view into a state a test needs instead of choreographing the UI "
            + "into it. Identify the element by a path from list_ui_elements, a plain element name, or a path "
            + "expression; name the component by its type name (GraphicRaycaster) or full name, and the "
            + "property by a dotted path. Components are a uGUI concept, so this works on ugui: elements only — a uitk: "
            + "element has none and is refused. Strings, booleans, numbers and enums (by member name or number) convert; "
            + "anything else is refused by name. A member with no setter, or one reached through a struct read by "
            + "value (where the write would be lost), is refused rather than silently dropped. This writes straight to "
            + "the object and bypasses whatever invariants the owning code maintains — prefer driving the real UI.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.String("element", "Path from list_ui_elements, a plain element name, or a path expression (//Panel//Button, Grid/Item[2] — indices are zero-based, so Item[0] is the first).", isRequired: true)
                  .String("component", "Component type name or full name, e.g. GraphicRaycaster or UnityEngine.UI.GraphicRaycaster.", isRequired: true)
                  .String("property", "Property or field to write, dotted for nested writes, e.g. enabled or targetGraphic.enabled. A step through a struct (color.a) is refused, since the write would land on a copy.", isRequired: true)
                  .Any("value", "The value to write: a JSON string, number, boolean, or an enum member name. Send an explicit null to write null.", isRequired: true);

        public override McpToolAnnotations Annotations => McpToolAnnotations.Mutating(destructive: true, idempotent: true);

        public override UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            string element = arguments.GetString("element", string.Empty);
            string componentName = arguments.GetString("component", string.Empty);
            string propertyPath = arguments.GetString("property", string.Empty);

            // The converter reads an absent argument as an explicit JSON null, so this is what keeps a call that
            // forgot value from writing null to the member and reporting written:true.
            JToken? value = arguments["value"];

            if (value == null)
                return UniTask.FromResult(McpToolResult.Error("Provide value (the value to write). Send an explicit null to write null."));

            if (!UiAutomation.TryResolveGameObject(element, out GameObject gameObject, out string path))
                return UniTask.FromResult(ResolveFailure(element));

            if (!ComponentProperty.TryFindComponent(gameObject, componentName, out Component? component, out string missing))
                return UniTask.FromResult(McpToolResult.Error(missing));

            if (!ComponentProperty.TryWrite(component, propertyPath, value, out object? written, out string error))
                return UniTask.FromResult(McpToolResult.Error(error));

            var result = new JObject
            {
                ["path"] = path,
                ["component"] = component.GetType().FullName,
                ["property"] = propertyPath,
                ["value"] = ComponentProperty.ToToken(written),
                ["written"] = true,
            };

            return UniTask.FromResult(McpToolResult.Json(result));
        }

        /// <summary>
        ///     Tells "nothing matched" apart from "matched a UI-Toolkit element". Both fail
        ///     <see cref="UiAutomation.TryResolveGameObject" />, which resolves uGUI only, but only the first is an
        ///     absence: answering both with the generic miss sends an agent back to a listing that does contain the
        ///     element it just named.
        /// </summary>
        private static McpToolResult ResolveFailure(string element) =>
            UiAutomation.TryGetState(element, out _)
                ? McpToolResult.Error($"'{element}' is a UI-Toolkit element; set_component_property writes Unity Components and works on uGUI (ugui:) elements only.")
                : UiAutomation.NotFound(element);
    }
}
#endif
