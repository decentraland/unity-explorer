#if MCP_TEST_AUTOMATION
using Cysharp.Threading.Tasks;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using Newtonsoft.Json.Linq;
using System.Threading;

namespace DCL.McpServer.Tools
{
    public class SetUiTextTool : McpTool
    {
        public override string Name => "set_ui_text";

        public override string Description =>
            "Type into a client UI text field — a uGUI InputField, a TextMeshPro TMP_InputField or a UI-Toolkit "
            + "TextField. Identify the field by a path from list_ui_elements, a plain "
            + "element name, or a path expression. The field's value-changed notification fires as it does "
            + "for a real edit; submit (default true) additionally raises the end-edit/submit notification, which is what "
            + "pressing Enter in the field does and what the login email, OTP code, username and search fields need. "
            + "Returns the field's post-edit state with applied:true, or applied:false with the reason when the resolved "
            + "element is not a text field.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.String("element", "Path from list_ui_elements, a plain element name, or a path expression (//Panel//Button, Grid/Item[2] — indices are zero-based, so Item[0] is the first).", isRequired: true)
                  .String("text", "The value to write into the field. An empty string clears it.", isRequired: true)
                  .Boolean("submit", "Also raise the field's end-edit/submit notification, like pressing Enter. Default true.");

        public override McpToolAnnotations Annotations => McpToolAnnotations.Mutating(destructive: false, idempotent: true);

        public override UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            string element = arguments.GetString("element", string.Empty);

            if (arguments["text"]?.Type != JTokenType.String)
                return UniTask.FromResult(McpToolResult.Error("Provide text (the string to write into the field)."));

            string text = arguments.GetString("text", string.Empty);
            bool submit = arguments.GetBool("submit", true);

            if (!UiAutomation.TrySetText(element, text, submit, out JObject result))
                return UniTask.FromResult(UiAutomation.NotFound(element));

            return UniTask.FromResult(McpToolResult.Json(result));
        }
    }
}
#endif
