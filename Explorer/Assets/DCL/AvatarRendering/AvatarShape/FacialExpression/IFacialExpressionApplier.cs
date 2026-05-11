namespace DCL.AvatarRendering.AvatarShape.FacialExpression
{
    /// <summary>
    ///     Single seam between non-ECS layers (wheel controller) and the player entity:
    ///     writes a <see cref="Components.TriggerFacialExpressionIntent"/> the next frame.
    ///     Lets callers stay world-free.
    /// </summary>
    public interface IFacialExpressionApplier
    {
        void Apply(byte eyebrowsIndex, byte eyesIndex, byte mouthIndex);
    }
}