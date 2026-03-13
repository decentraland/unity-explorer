namespace DCL.AvatarRendering.AvatarShape
{
    /// <summary>
    ///     Shared mutable state written by the "Avatar Face" debug widget and read by
    ///     <c>AvatarFacialExpressionSystem</c> to override the expression on all avatars.
    ///     Set <see cref="IsDirty"/> to true after modifying the indices to trigger an update.
    /// </summary>
    public class AvatarFaceDebugData
    {
        public int EyebrowsIndex;
        public int EyesIndex;
        public int MouthIndex;
        public bool IsDirty;

        /// <summary>
        ///     When true, <c>AvatarFacialExpressionSystem</c> will start a blink on all avatars this frame.
        /// </summary>
        public bool TriggerBlink;
    }
}
