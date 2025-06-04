using DCL.ECSComponents;
using DG.Tweening;
using System.Collections.Generic;

namespace DCL.SDKComponents.Tween.Systems
{
    public static class EasingFunctionsMap
    {
        public static readonly Dictionary<EasingFunction, Ease> TO_EASING_FUNCTION = new ()
        {
            [EasingFunction.EfLinear] = Ease.Linear,
            [EasingFunction.EfEaseinsine] = Ease.InSine,
            [EasingFunction.EfEaseoutsine] = Ease.OutSine,
            [EasingFunction.EfEasesine] = Ease.InOutSine,
            [EasingFunction.EfEaseinquad] = Ease.InQuad,
            [EasingFunction.EfEaseoutquad] = Ease.OutQuad,
            [EasingFunction.EfEasequad] = Ease.InOutQuad,
            [EasingFunction.EfEaseinexpo] = Ease.InExpo,
            [EasingFunction.EfEaseoutexpo] = Ease.OutExpo,
            [EasingFunction.EfEaseexpo] = Ease.InOutExpo,
            [EasingFunction.EfEaseinelastic] = Ease.InElastic,
            [EasingFunction.EfEaseoutelastic] = Ease.OutElastic,
            [EasingFunction.EfEaseelastic] = Ease.InOutElastic,
            [EasingFunction.EfEaseinbounce] = Ease.InBounce,
            [EasingFunction.EfEaseoutbounce] = Ease.OutBounce,
            [EasingFunction.EfEasebounce] = Ease.InOutBounce,
            [EasingFunction.EfEaseincubic] = Ease.InCubic,
            [EasingFunction.EfEaseoutcubic] = Ease.OutCubic,
            [EasingFunction.EfEasecubic] = Ease.InOutCubic,
            [EasingFunction.EfEaseinquart] = Ease.InQuart,
            [EasingFunction.EfEaseoutquart] = Ease.OutQuart,
            [EasingFunction.EfEasequart] = Ease.InOutQuart,
            [EasingFunction.EfEaseinquint] = Ease.InQuint,
            [EasingFunction.EfEaseoutquint] = Ease.OutQuint,
            [EasingFunction.EfEasequint] = Ease.InOutQuint,
            [EasingFunction.EfEaseincirc] = Ease.InCirc,
            [EasingFunction.EfEaseoutcirc] = Ease.OutCirc,
            [EasingFunction.EfEasecirc] = Ease.InOutCirc,
            [EasingFunction.EfEaseinback] = Ease.InBack,
            [EasingFunction.EfEaseoutback] = Ease.OutBack,
            [EasingFunction.EfEaseback] = Ease.InOutBack,
        };
    }
}
