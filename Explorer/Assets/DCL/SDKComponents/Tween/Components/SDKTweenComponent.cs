using CrdtEcsBridge.Components.Transform;
using DCL.ECSComponents;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using ECS.Unity.Transforms.Components;
using UnityEngine;

namespace DCL.SDKComponents.Tween.Components
{
    public class Vector3Tweener
    {
        public Vector3 startValue = new (0, 0, 0);
        public Vector3 endValue = new (10, 10, 10);
        public float duration = 5f;

        public Vector3 currentValue;

        private TweenerCore<Vector3, Vector3, VectorOptions> core;

        public void TransformVector3(Vector3 start, Vector3 end, float duration, Ease ease)
        {
            currentValue = start;
            core = DOTween.To(() => currentValue, x => currentValue = x, end, duration)
                .SetEase(ease)
                .SetAutoKill(false)
                .OnUpdate(() =>
                {
                    // This code is executed every frame while the tween is running
                    Debug.Log("Current Value: " + currentValue);

                    // You can add any logic here that you need to execute every frame
                    // For example, updating a UI element or another variable
                })
                .OnComplete(() =>
                {
                    // This code is executed once when the tween completes
                    Debug.Log("Tween completed");
                });
        }

        public float ElapsedPercentage()
        {
            if (core != null)
                return core.ElapsedPercentage();
            return 0;
        }
    }
    
    
    public struct SDKTweenComponent
    {
        public bool IsDirty { get; set; }
        public bool IsPlaying { get; set; }
        public float CurrentTime { get; set; }
        public Tweener Tweener { get; set; }
        public SDKTransform HelperSDKTransform { get; set; }
        public TweenStateStatus TweenStateStatus { get; set; }
        public Vector3Tweener Vector3Tweener { get; set; }
    }
    
}
