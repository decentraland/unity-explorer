using System;
using DCL.ECSComponents;
using NUnit.Framework.Constraints;

namespace DCL.SDKComponents.Tween.Components
{
    public struct SDKTweenComponent : IDisposable
    {
        public bool IsDirty { get; set; }
        public TweenStateStatus TweenStateStatus { get; set; }
        public ICustomTweener CustomTweener { get; set; }


        public void Dispose()
        {
            if (CustomTweener != null)
                CustomTweener.Kill();
        }

        public bool IsActive()
        {
            return CustomTweener != null && CustomTweener.IsActive();
        }

        public void Rewind()
        {
            CustomTweener.Pause();
            CustomTweener.Rewind();
        }
    }
    
}
