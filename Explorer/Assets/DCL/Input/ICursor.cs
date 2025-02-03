namespace DCL.Input
{
    public interface ICursor
    {
        bool IsLocked();

        void Lock();

        void Unlock();

        void SetVisibility(bool visible);

        void SetStyle(CursorStyle style, bool force = false);

        /// <summary>
        ///    Indicates whether the Style of the cursor will be handled by the ECS system
        ///     or if it will ignore it and keep the style set when setting it with a forced flag
        /// </summary>
        bool IsStyleForced { get; }
    }
}
