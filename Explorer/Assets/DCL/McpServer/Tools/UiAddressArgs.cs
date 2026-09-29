using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using DCL.SyntheticInput.UiSimulation;
using Newtonsoft.Json.Linq;
using System.Diagnostics.CodeAnalysis;

namespace DCL.McpServer.Tools
{
    /// <summary>Wire-facing UI stack selector shared by the ui_* tools.</summary>
    public enum UiStackWire : byte
    {
        Ugui,
        Sdk,
    }

    /// <summary>Shared argument parsing for the ui_* tools: one element address from stack + path/id/altId/crdtId.</summary>
    internal static class UiAddressArgs
    {
        public const string ADDRESS_SCHEMA_HINT =
            "Address one element: ugui elements by 'path' (from ui_list), 'id' (from the last ui_list) or 'altId'; sdk elements by 'crdtId'.";

        public static McpJsonSchema DescribeAddress(McpJsonSchema schema) =>
            schema.Enum<UiStackWire>("stack", "Which UI stack the element lives in. Default ugui (the client interface); sdk is the scene's own UI.")
                  .String("path", "ugui: transform path from ui_list, e.g. 'MainUI/Sidebar/ExploreButton' ('[n]' suffix disambiguates same-named siblings).")
                  .Integer("id", "ugui: element id from the most recent ui_list call (stale after UI changes).")
                  .String("altId", "ugui: AltId locator (ALTTESTER builds only).")
                  .Integer("crdtId", "sdk: the UI entity's CRDT id (from ui_list or the scene code).");

        public static bool TryParse(JObject arguments, out UiElementAddress address, [NotNullWhen(false)] out string? error)
        {
            address = default(UiElementAddress);
            error = null;

            if (!arguments.TryGetEnum("stack", UiStackWire.Ugui, out UiStackWire stack))
            {
                error = arguments.EnumArgumentError<UiStackWire>("stack");
                return false;
            }

            if (stack == UiStackWire.Sdk)
            {
                if (!arguments.TryGetInt("crdtId", out int crdtId))
                {
                    error = "sdk addressing requires crdtId.";
                    return false;
                }

                address = UiElementAddress.Sdk(crdtId);
                return true;
            }

            if (arguments["id"] is { Type: JTokenType.Integer } idToken)
            {
                address = UiElementAddress.UguiInstance(idToken.Value<ulong>());
                return true;
            }

            if (arguments.GetStringOrNull("altId") is { } altId)
            {
                address = UiElementAddress.UguiAltId(altId);
                return true;
            }

            if (arguments.GetStringOrNull("path") is { } path)
            {
                address = UiElementAddress.UguiPath(path);
                return true;
            }

            error = "ugui addressing requires one of: path, id, altId.";
            return false;
        }
    }
}
