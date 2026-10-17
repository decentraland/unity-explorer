using Cysharp.Threading.Tasks;
using DCL.UI;
using System.Threading;
using UnityEngine;

namespace MVC
{
    public abstract class ViewBaseWithAnimationElement : ViewBase
    {
        [field: SerializeField] private ViewAnimationElementBase viewAnimationElement { get; set;}

        protected override UniTask PlayShowAnimationAsync(CancellationToken ct) =>
            viewAnimationElement.PlayShowAnimation(ct);

        protected override UniTask PlayHideAnimationAsync(CancellationToken ct) =>
            viewAnimationElement.PlayHideAnimation(ct);
    }
}
