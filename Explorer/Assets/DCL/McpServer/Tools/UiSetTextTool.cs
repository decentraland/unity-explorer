using Cysharp.Threading.Tasks;
using DCL.McpServer.Utils;
using DCL.SyntheticInput.UiSimulation;
using Newtonsoft.Json.Linq;
using System.Threading;
using UnityEngine;

namespace DCL.McpServer.Tools
{
    /// <summary>Types into a UI text input (or selects an SDK dropdown option) semantically, firing the same value/submit events a user produces.</summary>
    public class UiSetTextTool : McpTool
    {
        private readonly UiAutomationServices uiAutomation;

        public override string Name => "ui_set_text";

        public override string Description =>
            "Set the text of a UI input field, firing the same value-changed (and optionally submit) events typing produces. "
            + UiAddressArgs.ADDRESS_SCHEMA_HINT + " For SDK dropdowns pass optionIndex instead of text.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            UiAddressArgs.DescribeAddress(schema)
                         .String("text", "The text to set.")
                         .Boolean("submit", "Also fire the submit event (Enter). Default false.")
                         .Integer("optionIndex", "sdk dropdowns: select this option index instead of setting text.");

        public override McpToolAnnotations Annotations => McpToolAnnotations.Mutating(destructive: false, idempotent: false);

        public UiSetTextTool(UiAutomationServices uiAutomation)
        {
            this.uiAutomation = uiAutomation;
        }

        public override UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            if (!UiAddressArgs.TryParse(arguments, out UiElementAddress address, out string? addressError))
                return UniTask.FromResult(McpToolResult.Error(addressError!));

            bool hasText = arguments["text"]?.Type == JTokenType.String;
            bool hasOptionIndex = arguments.TryGetInt("optionIndex", out int optionIndex);

            if (!hasText && !hasOptionIndex)
                return UniTask.FromResult(McpToolResult.Error("Provide text, or optionIndex for an sdk dropdown."));

            bool submit = arguments.GetBool("submit", false);

            UiActionResult result;

            if (address.Stack == UiStack.SDK)
            {
                if (!uiAutomation.SdkResolver.TryResolve(address.CrdtId, out SdkUiElement element, out string? failure))
                    return UniTask.FromResult(McpToolResult.Error(failure!));

                result = hasOptionIndex
                    ? uiAutomation.Simulator.SelectDropdownSdk(element, optionIndex)
                    : uiAutomation.Simulator.SetTextSdk(element, arguments["text"]!.Value<string>()!, submit);
            }
            else
            {
                if (!hasText)
                    return UniTask.FromResult(McpToolResult.Error("optionIndex applies only to sdk dropdowns; ugui inputs take text."));

                if (!uiAutomation.Discovery.TryResolve(in address, out GameObject? target, out string? failure))
                    return UniTask.FromResult(McpToolResult.Error(failure!));

                result = uiAutomation.Simulator.SetTextUgui(target!, arguments["text"]!.Value<string>()!, submit);
            }

            return UniTask.FromResult(McpToolResult.Json(result.ToJson(uiAutomation.CursorStateName())));
        }
    }
}
