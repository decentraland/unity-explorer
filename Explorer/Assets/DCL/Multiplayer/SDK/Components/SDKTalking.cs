using DCL.ECSComponents;

namespace DCL.Multiplayer.SDK.Components
{
    /// <summary>
    ///     Voice activity of the player for <c>PBAvatarBase.talking</c>; <c>null</c> while it is not reported.
    ///     <see cref="Set" /> raises <see cref="IsDirty" /> only when the value changes.
    /// </summary>
    public struct SDKTalking : IDirtyMarker
    {
        public bool? Value { get; private set; }
        public bool IsDirty { get; set; }

        public void Set(bool? value)
        {
            if (Value == value) return;

            Value = value;
            IsDirty = true;
        }
    }
}
