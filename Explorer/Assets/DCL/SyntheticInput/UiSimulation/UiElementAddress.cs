using System;

namespace DCL.SyntheticInput.UiSimulation
{
    public enum UiStack : byte
    {
        /// <summary>Client uGUI interface (sidebar, chat, panels).</summary>
        UGUI,

        /// <summary>SDK scene UI (UI Toolkit), addressed by CRDT entity id.</summary>
        SDK,
    }

    /// <summary>
    ///     How a driver names a UI element. uGUI elements are addressed by transform path (with a "[n]" suffix
    ///     disambiguating same-named siblings), by the instance id a previous listing returned, or by AltId;
    ///     SDK scene UI is addressed by CRDT entity id only — element names do not exist in player builds.
    /// </summary>
    public readonly struct UiElementAddress
    {
        public readonly UiStack Stack;
        public readonly string? Path;

        /// <summary>The element's entity id (EntityId.ToULong) as reported by the last listing.</summary>
        public readonly ulong? InstanceId;
        public readonly string? AltId;
        public readonly int CrdtId;

        private UiElementAddress(UiStack stack, string? path, ulong? instanceId, string? altId, int crdtId)
        {
            Stack = stack;
            Path = path;
            InstanceId = instanceId;
            AltId = altId;
            CrdtId = crdtId;
        }

        public static UiElementAddress UguiPath(string path) =>
            new (UiStack.UGUI, path, null, null, -1);

        public static UiElementAddress UguiInstance(ulong instanceId) =>
            new (UiStack.UGUI, null, instanceId, null, -1);

        public static UiElementAddress UguiAltId(string altId) =>
            new (UiStack.UGUI, null, null, altId, -1);

        public static UiElementAddress Sdk(int crdtId) =>
            new (UiStack.SDK, null, null, null, crdtId);

        public override string ToString() =>
            Stack == UiStack.SDK
                ? $"sdk:crdt={CrdtId}"
                : AltId != null ? $"ugui:altId={AltId}"
                : InstanceId is { } id ? $"ugui:id={id}"
                : $"ugui:path={Path}";

        /// <summary>Strips the "(Clone)" suffix Unity appends to instantiated prefab roots.</summary>
        public static ReadOnlySpan<char> NormalizeName(string name) =>
            name.EndsWith("(Clone)", StringComparison.Ordinal) ? name.AsSpan(0, name.Length - 7) : name.AsSpan();

        /// <summary>Splits one path segment into its name and optional "[n]" sibling index.</summary>
        public static void ParseSegment(ReadOnlySpan<char> segment, out ReadOnlySpan<char> name, out int siblingIndex)
        {
            siblingIndex = 0;

            if (segment.Length > 2 && segment[^1] == ']')
            {
                int open = segment.LastIndexOf('[');

                if (open > 0 && int.TryParse(segment[(open + 1)..^1], out int parsed))
                {
                    name = segment[..open];
                    siblingIndex = parsed;
                    return;
                }
            }

            name = segment;
        }
    }
}
