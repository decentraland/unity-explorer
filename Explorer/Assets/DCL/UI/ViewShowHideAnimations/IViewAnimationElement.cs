using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.UI
{
    [Serializable]
    public abstract class ViewAnimationElementBase : MonoBehaviour, IViewAnimationElement
    {
        public abstract UniTask PlayShowAnimation(CancellationToken ct);

        public abstract UniTask PlayHideAnimation(CancellationToken ct);
    }

    public interface IViewAnimationElement
    {
        public UniTask PlayShowAnimation(CancellationToken ct);
        public UniTask PlayHideAnimation(CancellationToken ct);
    }
}
