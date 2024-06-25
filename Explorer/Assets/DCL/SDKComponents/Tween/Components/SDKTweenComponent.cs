using System;
using DCL.ECSComponents;

namespace DCL.SDKComponents.Tween.Components
{
    public struct SDKTweenComponent : IDisposable
    {
        public bool IsDirty { get; set; }
        public bool IsPlaying { get; set; }
        public TweenStateStatus TweenStateStatus { get; set; }
        public ICustomTweener CustomTweener { get; set; }

        public void Dispose()
        {
            if (CustomTweener != null)
                CustomTweener.Kill();
        }
    }
    
}
