namespace DCL.Multiplayer.SDK.Components
{
    /// <summary>
    ///     Local player's Nearby voice activity, exposed to every scene as <c>PBAvatarBase.talking</c>.
    ///     Present on the global-world local player entity only while Nearby voice chat is enabled.
    /// </summary>
    public struct TalkingComponent
    {
        public bool IsTalking;
    }
}
