#if MCP_TEST_AUTOMATION
using Cysharp.Threading.Tasks;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using Newtonsoft.Json.Linq;
using System.Threading;

namespace DCL.McpServer.Tools
{
    public class ClickUiTool : McpTool
    {
        public override string Name => "click_ui";

        public override string Description =>
            "Click a real client UI control the way a user does. Identify it by a path from "
            + "list_ui_elements, a plain element name, or a path expression (//Panel//Button). The click is hit-tested "
            + "through the live EventSystem at the element's screen position, so a control covered by a modal comes back "
            + "clicked:false with the blocker named instead of a false success; only an element with no raycastable "
            + "graphic falls back to a direct dispatch (reported as dispatch:direct). UI-Toolkit controls receive a "
            + "navigation-submit. Read the effect back with get_ui_state or screenshot.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.String("element", "Path from list_ui_elements, a plain element name, or a path expression (//Panel//Button, Grid/Item[2] — indices are zero-based, so Item[0] is the first).", isRequired: true);

        public override McpToolAnnotations Annotations => McpToolAnnotations.Mutating(destructive: false, idempotent: false);

        public override UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            string element = arguments.GetString("element", string.Empty);

            if (!UiAutomation.TryClick(element, out JObject result))
                return UniTask.FromResult(UiAutomation.NotFound(element));

            return UniTask.FromResult(McpToolResult.Json(result));
        }
    }
}
#endif
