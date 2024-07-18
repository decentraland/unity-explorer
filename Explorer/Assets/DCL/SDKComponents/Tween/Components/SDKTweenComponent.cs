using DCL.ECSComponents;

namespace DCL.SDKComponents.Tween.Components
{
    public struct SDKTweenComponent
    {

        public bool IsDirty { get; set; }
        public TweenStateStatus TweenStateStatus { get; set; }
        public ICustomTweener CustomTweener { get; set; }

        public bool IsActive()
        {
            return CustomTweener != null && CustomTweener.IsActive();
        }

        public void Clear()
        {
            IsDirty = false;
            TweenStateStatus = TweenStateStatus.TsCompleted;
            CustomTweener?.Clear();
            CustomTweener = null;
        }

        public void Rewind()
        {
            CustomTweener.Pause();
            CustomTweener.Rewind();
        }
    }
}
