using DCL.ECSComponents;

namespace DCL.SDKComponents.SceneUI.Components
{
    /// <summary>
    ///     How many pointer-event results the scene's write pass appended for the element, per event type, since the
    ///     pooled component was initialized. A queued event the entity does not declare is cleared without one. The
    ///     automation layer reads it to tell an event the scene received from one that produced no result.
    /// </summary>
    public struct PointerEventDrainLog
    {
        private int upAppended;
        private int downAppended;
        private int hoverEnterAppended;
        private int hoverLeaveAppended;

        public readonly int AppendedCount(PointerEventType type) =>
            type switch
            {
                PointerEventType.PetUp => upAppended,
                PointerEventType.PetDown => downAppended,
                PointerEventType.PetHoverEnter => hoverEnterAppended,
                _ => hoverLeaveAppended,
            };

        public void RecordAppended(PointerEventType type)
        {
            switch (type)
            {
                case PointerEventType.PetUp:
                    upAppended++;
                    break;
                case PointerEventType.PetDown:
                    downAppended++;
                    break;
                case PointerEventType.PetHoverEnter:
                    hoverEnterAppended++;
                    break;
                default:
                    hoverLeaveAppended++;
                    break;
            }
        }
    }
}
