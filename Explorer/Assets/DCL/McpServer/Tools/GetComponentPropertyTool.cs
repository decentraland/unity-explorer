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
    ///     Reads one property off one component of a resolved UI element. Polling it is how a suite waits for a
    ///     view to reach a state that has no other observable signal.
    /// </summary>
    public class GetComponentPropertyTool : McpTool
    {
        public override string Name => "get_component_property";

        public override string Description =>
            "Read a property off a component of a client UI element — the readiness signals a UI suite gates on "
            + "(GraphicRaycaster.enabled to know a panel accepts clicks, a view's IsLoading or ItemId). Identify the "
            + "element by a path from list_ui_elements, a plain element name, or a path expression; name "
            + "the component by its type name (GraphicRaycaster) or full name (UnityEngine.UI.GraphicRaycaster), and the "
            + "property by a dotted path (enabled, rectTransform.rect.width). Components are a uGUI concept, so this "
            + "works on ugui: elements only — a uitk: element has none and is refused. Poll it to wait for a view to "
            + "reach a state. Primitives come back typed; anything else comes back as its string form.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.String("element", "Path from list_ui_elements, a plain element name, or a path expression (//Panel//Button, Grid/Item[2] — indices are zero-based, so Item[0] is the first).", isRequired: true)
                  .String("component", "Component type name or full name, e.g. GraphicRaycaster or UnityEngine.UI.GraphicRaycaster.", isRequired: true)
                  .String("property", "Property or field to read, dotted for nested reads, e.g. enabled or rectTransform.rect.width.", isRequired: true);

        public override McpToolAnnotations Annotations => McpToolAnnotations.ReadOnly();

        public override UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            string element = arguments.GetString("element", string.Empty);
            string componentName = arguments.GetString("component", string.Empty);
            string propertyPath = arguments.GetString("property", string.Empty);

            if (!UiAutomation.TryResolveGameObject(element, out GameObject gameObject, out string path))
                return UniTask.FromResult(ResolveFailure(element));

            if (!ComponentProperty.TryFindComponent(gameObject, componentName, out Component? component, out string missing))
                return UniTask.FromResult(McpToolResult.Error(missing));

            if (!ComponentProperty.TryRead(component, propertyPath, out object? value, out string error))
                return UniTask.FromResult(McpToolResult.Error(error));

            var result = new JObject
            {
                ["path"] = path,
                ["component"] = component.GetType().FullName,
                ["property"] = propertyPath,
                ["value"] = ComponentProperty.ToToken(value),
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
                ? McpToolResult.Error($"'{element}' is a UI-Toolkit element; get_component_property reads Unity Components and works on uGUI (ugui:) elements only.")
                : UiAutomation.NotFound(element);
    }
}
#endif
