using DCL.ECSComponents;

namespace DCL.Multiplayer.SDK.Components
{
    /// <summary>
    ///     Voice activity of the player for <c>PBAvatarBase.talking</c>; absent while it is not reported.
    ///     <see cref="Set" /> raises <see cref="IsDirty" /> only when the value changes.
    /// </summary>
    public struct SDKTalking : IDirtyMarker
    {
        public bool IsTalking { get; private set; }
        public bool IsDirty { get; set; }

        public SDKTalking(bool isTalking)
        {
            IsTalking = isTalking;
            IsDirty = true;
        }

        public void Set(bool isTalking)
        {
            if (IsTalking == isTalking) return;

            IsTalking = isTalking;
            IsDirty = true;
        }
    }
}
