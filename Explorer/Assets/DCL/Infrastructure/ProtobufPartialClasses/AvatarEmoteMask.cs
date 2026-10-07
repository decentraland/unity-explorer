namespace DCL.ECSComponents
{
    /// <summary>
    ///     Which bones an emote animation applies to. The numeric values are the encoding carried on
    ///     the wire by <c>PlayerEmote.mask</c> (comms) and <c>EmoteStart.mask</c> (Pulse), so they
    ///     must not change.
    /// </summary>
    public enum AvatarEmoteMask
    {
        AemFullBody = 0,
        AemUpperBody = 1,
    }
}
