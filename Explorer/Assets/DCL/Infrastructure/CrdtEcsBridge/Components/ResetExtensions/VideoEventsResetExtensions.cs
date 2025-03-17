using DCL.ECSComponents;

namespace CrdtEcsBridge.Components.ResetExtensions
{
    public static class VideoEventsResetExtensions
    {
        public static void Reset(this PBVideoEvent videoEvent)
        {
            videoEvent.State = VideoState.VsNone;
            videoEvent.Timestamp = 0;
            videoEvent.TickNumber = 0;
            videoEvent.CurrentOffset = -1f;
            videoEvent.VideoLength = -1f;
        }
    }
}
