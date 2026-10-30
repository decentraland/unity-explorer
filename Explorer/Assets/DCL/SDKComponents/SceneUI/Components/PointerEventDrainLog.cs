using DCL.ECSComponents;

namespace DCL.SDKComponents.SceneUI.Components
{
    /// <summary>
    ///     What happened to each pointer event the element's single slot held: written by the scene's pointer-event
    ///     system whenever it drains the slot, matched to a declared entry or not. The automation layer reads it to tell
    ///     an event the scene received from one the slot dropped.
    /// </summary>
    public struct PointerEventDrainLog
    {
        /// <summary>Drains of the slot, matched or not.</summary>
        public int DrainCount;

        /// <summary>Drains that appended a pointer-event result for the scene.</summary>
        public int AppendCount;

        public PointerEventType? LastDrained;

        public bool LastWasAppended;

        public void Record(PointerEventType drained, bool appended)
        {
            DrainCount++;
            LastDrained = drained;
            LastWasAppended = appended;

            if (appended)
                AppendCount++;
        }
    }
}
