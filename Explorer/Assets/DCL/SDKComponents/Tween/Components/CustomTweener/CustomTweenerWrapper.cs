using DCL.ECSComponents;

namespace DCL.SDKComponents.Tween.Components
{
    public class CustomTweenerWrapper
    {
        public TweenResult tweenResult;
        private ICustomTweener customTweener;

        public void Initialize(PBTween pbTween)
        {
            var tweenResult = new TweenResult();
            switch (pbTween.ModeCase)
            {
                case PBTween.ModeOneofCase.Move:

                    break;
                case PBTween.ModeOneofCase.Rotate:
                    break;
                case PBTween.ModeOneofCase.Scale:
                    break;
            }
        }
    }
}