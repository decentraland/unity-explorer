using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.CustomPlugins;
using DG.Tweening.Plugins.Options;
using UnityEngine;

namespace DCL.SDKComponents.Tween.Components
{
    public class Vector3Tweener : CustomTweener<Vector3, VectorOptions>
    {
        protected sealed override TweenerCore<Vector3, Vector3, VectorOptions> CreateTweener(Vector3 start, Vector3 end, float duration)
        {
            CurrentValue = start;
            return DOTween.To(() => CurrentValue, x => CurrentValue = x, end, duration);
        }
    }

    public class QuaternionTweener : CustomTweener<Quaternion, NoOptions>
    {
        protected override TweenerCore<Quaternion, Quaternion, NoOptions> CreateTweener(Quaternion start, Quaternion end, float duration)
        {
            CurrentValue = start;
            return DOTween.To(PureQuaternionPlugin.Plug(), () => CurrentValue, x => CurrentValue = x, end, duration);
        }
    }

    public class Vector2Tweener : CustomTweener<Vector2, VectorOptions>
    {
        protected sealed override TweenerCore<Vector2, Vector2, VectorOptions> CreateTweener(Vector2 start, Vector2 end, float duration)
        {
            CurrentValue = start;
            return DOTween.To(() => CurrentValue, x => CurrentValue = x, end, duration);
        }
    }
}
