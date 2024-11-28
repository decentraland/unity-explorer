using DCL.ECSComponents;

namespace DCL.SDKComponents.Tween.Components
{
    public struct SDKTweenComponent<T>
    {
        public bool IsDirty { get; set; }
        public TweenStateStatus TweenStateStatus { get; set; }
        public ICustomTweener<T> CustomTweener { get; set; }

        public bool IsActive() =>
            CustomTweener != null && CustomTweener.IsActive();

        public void Rewind()
        {
            CustomTweener.Pause();
            CustomTweener.Rewind();
        }
    }
}
