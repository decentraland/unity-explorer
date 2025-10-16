using DCL.ECSComponents;

namespace DCL.SDKComponents.Tween.Components
{
    public struct SDKTweenSequenceComponent
    {
        public bool IsDirty { get; set; }
        public TweenStateStatus TweenStateStatus { get; set; }
        public ITweener SequenceTweener { get; set; }

        public bool IsActive() =>
            SequenceTweener != null && SequenceTweener.IsActive();

        public void Rewind()
        {
            SequenceTweener.Pause();
            SequenceTweener.Rewind();
        }
    }
}





