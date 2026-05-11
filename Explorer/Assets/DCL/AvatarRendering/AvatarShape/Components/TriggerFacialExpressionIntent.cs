namespace DCL.AvatarRendering.AvatarShape.Components
{
    /// <summary>
    ///     One-frame intent placed on the local player to apply a face pose. The shape mirrors the
    ///     network payload (3 atlas indices) so the wheel/shortcut writers don't need the
    ///     <c>AvatarFaceExpressionDefinition</c> table — they translate slot → indices upstream.
    ///     Consumed by <see cref="ApplyFacialExpressionIntentSystem"/>.
    /// </summary>
    public struct TriggerFacialExpressionIntent
    {
        public byte EyebrowsIndex;
        public byte EyesIndex;
        public byte MouthIndex;
    }
}