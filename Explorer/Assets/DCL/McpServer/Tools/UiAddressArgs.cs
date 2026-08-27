using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using DCL.SyntheticInput.UiSimulation;
using Newtonsoft.Json.Linq;

namespace DCL.McpServer.Tools
{
    /// <summary>Wire-facing UI stack selector shared by the ui_* tools.</summary>
    public enum UiStackWire : byte
    {
        UGUI,
        SDK,
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

        public static bool TryParse(JObject arguments, out UiElementAddress address, out string? error)
        {
            address = default(UiElementAddress);
            error = null;

            if (!arguments.TryGetEnum("stack", UiStackWire.UGUI, out UiStackWire stack))
            {
                error = "stack must be one of: ugui, sdk.";
                return false;
            }

            if (stack == UiStackWire.SDK)
            {
                if (!arguments.TryGetInt("crdtId", out int crdtId))
                {
                    error = "sdk addressing requires crdtId.";
                    return false;
                }

                address = UiElementAddress.Sdk(crdtId);
                return true;
            }

            if (arguments["id"]?.Type == JTokenType.Integer)
            {
                address = UiElementAddress.UguiInstance(arguments["id"]!.Value<ulong>());
                return true;
            }

            if (arguments["altId"]?.Type == JTokenType.String)
            {
                address = UiElementAddress.UguiAltId(arguments["altId"]!.Value<string>()!);
                return true;
            }

            if (arguments["path"]?.Type == JTokenType.String)
            {
                address = UiElementAddress.UguiPath(arguments["path"]!.Value<string>()!);
                return true;
            }

            error = "ugui addressing requires one of: path, id, altId.";
            return false;
        }
    }
}
