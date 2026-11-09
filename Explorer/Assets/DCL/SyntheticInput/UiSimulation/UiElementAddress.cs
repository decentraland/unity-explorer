using System;

namespace DCL.SyntheticInput.UiSimulation
{
    public enum UiStack : byte
    {
        UGUI,

        /// <summary>SDK scene UI, built with UI Toolkit.</summary>
        SDK,
    }

    /// <summary>
    ///     How a driver names a UI element:
    ///     - uGUI: a transform path ("[n]" suffix for same-named siblings), the id from a previous listing, or an AltId
    ///     - SDK scene UI: the CRDT entity id only, because element names carry the entity only in Editor builds
    /// </summary>
    public readonly struct UiElementAddress
    {
        public readonly UiStack Stack;
        public readonly string? Path;

        /// <summary>EntityId.ToULong of the element, as reported by the last listing.</summary>
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

        public static ReadOnlySpan<char> NormalizeName(string name) =>
            name.EndsWith("(Clone)", StringComparison.Ordinal) ? name.AsSpan(0, name.Length - 7) : name.AsSpan();

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
