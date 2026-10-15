namespace DCL.Multiplayer.SDK.Components
{
    /// <summary>
    ///     Voice activity of the player written as <c>PBAvatarBase.talking</c>; <c>null</c> while the engine does not report it.
    ///     <see cref="IsDirty" /> is reset by <see cref="DCL.Multiplayer.SDK.Systems.SceneWorld.CleanUpAvatarPropagationComponentsSystem" />.
    /// </summary>
    public struct SDKTalking
    {
        public bool? Value;
        public bool IsDirty;

        public void Set(bool? value)
        {
            if (Value == value) return;

            Value = value;
            IsDirty = true;
        }
    }
}
