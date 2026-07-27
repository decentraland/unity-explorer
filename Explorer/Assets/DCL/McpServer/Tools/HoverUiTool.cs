#if MCP_TEST_AUTOMATION
using Cysharp.Threading.Tasks;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using Newtonsoft.Json.Linq;
using System.Threading;

namespace DCL.McpServer.Tools
{
    public class HoverUiTool : McpTool
    {
        public override string Name => "hover_ui";

        public override string Description =>
            "Move the pointer onto a client UI control, so hover-only state — a tooltip, a highlight, a row's reveal-on-hover "
            + "buttons — becomes visible and assertable. Identify it by a path from list_ui_elements, a plain element name, "
            + "or a path expression (//Panel//Button). Hit-tested through the live EventSystem exactly like click_ui, so a "
            + "control covered by a modal comes back hovered:false with the blocker named rather than reporting a hover a "
            + "user could not produce. The previously hovered element is exited first, so hovering one element after another "
            + "leaves no stale highlight. The pointer stays there until the next hover_ui; read the effect back with "
            + "get_ui_state, list_ui_elements or screenshot.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.String("element", "Path from list_ui_elements, a plain element name, or a path expression (//Panel//Button, Grid/Item[2] — indices are zero-based, so Item[0] is the first).", isRequired: true);

        public override McpToolAnnotations Annotations => McpToolAnnotations.Mutating(destructive: false, idempotent: true);

        public override UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            string element = arguments.GetString("element", string.Empty);

            if (!UiAutomation.TryHover(element, out JObject result))
                return UniTask.FromResult(UiAutomation.NotFound(element));

            return UniTask.FromResult(McpToolResult.Json(result));
        }
    }
}
#endif
