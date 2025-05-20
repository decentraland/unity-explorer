using DCL.ECSComponents;

namespace DCL.SDKComponents.Tween.Components
{
    public struct SDKTweenComponent
    {
        public PBTween.ModeOneofCase TweenMode { get; set; }
        public bool IsDirty { get; set; }
        public TweenStateStatus TweenStateStatus { get; set; }
        public ITweener CustomTweener { get; set; }

        public ulong StartSyncedTimestamp;

        public bool IsActive() =>
            CustomTweener != null && CustomTweener.IsActive();

        public void Rewind()
        {
            CustomTweener.Pause();
            CustomTweener.Rewind();
        }
    }
}
