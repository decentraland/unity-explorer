using DCL.ECSComponents;

namespace DCL.SDKComponents.MediaStream
{
    public static class PBVideoEventExtensions
    {
        public static PBVideoEvent WithData(this PBVideoEvent pbVideoEvent, in MediaPlayerComponent mediaPlayer, uint tickNumber)
        {
            pbVideoEvent.State = mediaPlayer.State;
            pbVideoEvent.CurrentOffset = mediaPlayer.CurrentTime;
            pbVideoEvent.VideoLength = mediaPlayer.Duration;

            pbVideoEvent.Timestamp = tickNumber;
            pbVideoEvent.TickNumber = tickNumber;

            return pbVideoEvent;
        }
    }
}
