namespace SuperScrollView
{
    /// <summary>
    /// Direction along which a list or grid lays out its items. The numeric
    /// values are part of the serialization contract: prefabs persist
    /// <c>mArrangeType</c> as the underlying int, so the ordering is fixed and
    /// must not be reassigned.
    /// </summary>
    public enum ListItemArrangeType
    {
        TopToBottom = 0,
        BottomToTop = 1,
        LeftToRight = 2,
        RightToLeft = 3,
    }
}
