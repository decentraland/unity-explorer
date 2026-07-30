#if MCP_TEST_AUTOMATION
using Cysharp.Threading.Tasks;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using Newtonsoft.Json.Linq;
using System.Threading;

namespace DCL.McpServer.Tools
{
    public class ListUiElementsTool : McpTool
    {
        public override string Name => "list_ui_elements";

        public override string Description =>
            "List the client's live UI elements by walking the running UI hierarchy. Covers both UI "
            + "systems: every active node under each uGUI Canvas (controls and plain nodes alike, so a panel or a label "
            + "can be waited on) and every UI-Toolkit element under each active UIDocument. Each entry is {path, name, "
            + "type, system (ugui|uitk), interactable, visible, text?}; text reads TextMeshPro and legacy uGUI labels, "
            + "input-field values and toggle states — a field that masks its input on screen reads '<masked>' instead "
            + "of its value. Pass the returned path (or a plain element name, or a path "
            + "expression) to get_ui_state, click_ui, hover_ui, set_ui_text, scroll, get_component_property or "
            + "set_component_property. A full walk "
            + "is large — nameFilter keeps only elements whose name or path contains it (case-insensitive) and is the "
            + "way to stay under the result cap.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.String("nameFilter", "Case-insensitive substring; keep only elements whose name or path contains it.");

        public override McpToolAnnotations Annotations => McpToolAnnotations.ReadOnly();

        public override UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            string filter = arguments.GetString("nameFilter", string.Empty);

            JArray elements = UiAutomation.Enumerate(filter, out bool truncated);

            var output = new JObject
            {
                ["count"] = elements.Count,
                ["truncated"] = truncated,
                ["elements"] = elements,
            };

            return UniTask.FromResult(McpToolResult.Json(output));
        }
    }
}
#endif
