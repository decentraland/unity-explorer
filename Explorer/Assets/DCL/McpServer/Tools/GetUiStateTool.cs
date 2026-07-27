#if MCP_TEST_AUTOMATION
using Cysharp.Threading.Tasks;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using Newtonsoft.Json.Linq;
using System.Threading;

namespace DCL.McpServer.Tools
{
    public class GetUiStateTool : McpTool
    {
        public override string Name => "get_ui_state";

        public override string Description =>
            "Read one UI element's current state: {path, name, type, system, interactable, visible, text?}. Identify it "
            + "by a path from list_ui_elements, a plain element name, or a path expression "
            + "(//Panel//Button, Grid/Item[2], * wildcards); a sibling index is zero-based, so Item[0] is the first. "
            + "An error result means nothing in the live hierarchy matched "
            + "— that is how an absence check is expressed, so poll this to wait for a panel to appear or disappear. "
            + "A field that masks its input on screen reads '<masked>' instead of its value.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.String("element", "Path from list_ui_elements, a plain element name, or a path expression (//Panel//Button, Grid/Item[2] — indices are zero-based, so Item[0] is the first).", isRequired: true);

        public override McpToolAnnotations Annotations => McpToolAnnotations.ReadOnly();

        public override UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            string element = arguments.GetString("element", string.Empty);

            if (!UiAutomation.TryGetState(element, out JObject state))
                return UniTask.FromResult(UiAutomation.NotFound(element));

            return UniTask.FromResult(McpToolResult.Json(state));
        }
    }
}
#endif
