using Cysharp.Threading.Tasks;
using DCL.McpServer.Utils;
using DCL.SyntheticInput.UiSimulation;
using Newtonsoft.Json.Linq;
using System.Threading;

namespace DCL.McpServer.Tools
{
    /// <summary>Lists the interactable UI elements a driver can address with the other ui_* tools.</summary>
    public class UiListTool : McpTool
    {
        private enum ListStack : byte
        {
            ALL,
            UGUI,
            SDK,
        }

        private readonly UiAutomationServices uiAutomation;

        public override string Name => "ui_list";

        public override string Description =>
            "List the interactable UI elements on screen: client interface (ugui — buttons, toggles, inputs, scrolls, with "
            + "their address path) and the current scene's own UI (sdk — addressed by CRDT id). screenRect is in image "
            + "pixels (origin top-left, like a screenshot). Element ids stay valid until the next ui_list call.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.Enum<ListStack>("stack", "Which UI stack to list. Default all.")
                  .Boolean("checkOcclusion", "Also report whether each ugui element is covered at its center (runs one raycast per element). Default false.");

        public override McpToolAnnotations Annotations => McpToolAnnotations.ReadOnly();

        public UiListTool(UiAutomationServices uiAutomation)
        {
            this.uiAutomation = uiAutomation;
        }

        public override UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            if (!arguments.TryGetEnum("stack", ListStack.ALL, out ListStack stack))
                return UniTask.FromResult(McpToolResult.Error("stack must be one of: all, ugui, sdk."));

            bool checkOcclusion = arguments.GetBool("checkOcclusion", false);

            var elements = new JArray();

            if (stack != ListStack.SDK)
                foreach (JToken entry in uiAutomation.Discovery.ListInteractable(checkOcclusion))
                    elements.Add(entry);

            if (stack != ListStack.UGUI)
                foreach (JToken entry in uiAutomation.SdkResolver.ListInteractable())
                    elements.Add(entry);

            var result = new JObject
            {
                ["count"] = elements.Count,
                ["elements"] = elements,
            };

            return UniTask.FromResult(McpToolResult.Json(result));
        }
    }
}
