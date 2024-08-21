using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.UI
{
    [Serializable]
    public class FadeViewAnimationElement : ViewAnimationElementBase
    {
        [field: SerializeField] private float fadeTime { get; set; } = 0.3f;
        [field: SerializeField] private CanvasGroup canvasGroup { get; set; }

        public override UniTask PlayShowAnimation(CancellationToken ct)
        {
            canvasGroup.alpha = 0;
            return canvasGroup.DOFade(1, fadeTime).SetEase(Ease.Linear).ToUniTask(cancellationToken: ct);
        }

        public override UniTask PlayHideAnimation(CancellationToken ct) =>
            canvasGroup.DOFade(0, fadeTime).SetEase(Ease.Linear).ToUniTask(cancellationToken: ct);

    }
}
