using DCL.ECSComponents;

namespace CrdtEcsBridge.Components.ResetExtensions
{
    public static class PointerEventsResetExtensions
    {
        public static void Reset(this PBPointerEvents pbPointerEvents)
        {
            pbPointerEvents.PointerEvents.Clear();
            pbPointerEvents.AppendPointerEventResultsIntent.ValidIndices.Clear();
        }
    }
}
