#if MCP_TEST_AUTOMATION
using Cysharp.Threading.Tasks;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using Newtonsoft.Json.Linq;
using System.Threading;
using UnityEngine;

namespace DCL.McpServer.Tools
{
    public class ScrollTool : McpTool
    {
        private const float MAX_DELTA = 100f;

        public override string Name => "scroll";

        public override string Description =>
            "Send a mouse-wheel scroll at a client UI element, so a list can be paged to content that is not on screen "
            + "yet. Identify the element by a path from list_ui_elements, a plain element name, or a path "
            + "expression. The wheel notification is hit-tested exactly like click_ui and dispatched at the real top "
            + "hit, so it reaches the enclosing ScrollRect rather than a node that ignores it. deltaY is the usual "
            + "axis: positive scrolls up, negative down. scrolled:false means nothing there handled a wheel event.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.String("element", "Path from list_ui_elements, a plain element name, or a path expression (//Panel//Button, Grid/Item[2] — indices are zero-based, so Item[0] is the first).", isRequired: true)
                  .Number("deltaX", "Horizontal wheel amount. Default 0.")
                  .Number("deltaY", "Vertical wheel amount: positive scrolls up, negative down. Default -1.");

        public override McpToolAnnotations Annotations => McpToolAnnotations.Mutating(destructive: false, idempotent: false);

        public override UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            var delta = new Vector2(
                Mathf.Clamp(arguments.GetFloat("deltaX", 0f), -MAX_DELTA, MAX_DELTA),
                Mathf.Clamp(arguments.GetFloat("deltaY", -1f), -MAX_DELTA, MAX_DELTA));

            if (delta == Vector2.zero)
                return UniTask.FromResult(McpToolResult.Error("deltaX and deltaY must not both be zero."));

            string element = arguments.GetString("element", string.Empty);

            if (!UiAutomation.TryScroll(element, delta, out JObject result))
                return UniTask.FromResult(UiAutomation.NotFound(element));

            return UniTask.FromResult(McpToolResult.Json(result));
        }
    }
}
#endif
