using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.CustomPlugins;
using DG.Tweening.Plugins.Options;
using UnityEngine;

namespace DCL.SDKComponents.Tween.Components
{
    public class Vector3Tweener : CustomTweener<Vector3, VectorOptions>
    {
        protected override TweenerCore<Vector3, Vector3, VectorOptions> CreateTweener(DOGetter<Vector3> getter, DOSetter<Vector3> setter, Vector3 end, float duration) =>
            DOTween.To(getter, setter, end, duration);
    }

    public class QuaternionTweener : CustomTweener<Quaternion, NoOptions>
    {
        protected override TweenerCore<Quaternion, Quaternion, NoOptions> CreateTweener(DOGetter<Quaternion> getter, DOSetter<Quaternion> setter, Quaternion end, float duration) =>
            DOTween.To(PureQuaternionPlugin.Plug(), getter, setter, end, duration);
    }

    public class Vector2Tweener : CustomTweener<Vector2, VectorOptions>
    {
        protected override TweenerCore<Vector2, Vector2, VectorOptions> CreateTweener(DOGetter<Vector2> getter, DOSetter<Vector2> setter, Vector2 end, float duration) =>
            DOTween.To(getter, setter, end, duration);
    }
}
