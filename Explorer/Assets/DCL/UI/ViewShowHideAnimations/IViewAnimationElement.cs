using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.UI
{
    [Serializable]
    public abstract class ViewAnimationElementBase : MonoBehaviour
    {
        public abstract UniTask PlayShowAnimation(CancellationToken ct);

        public abstract UniTask PlayHideAnimation(CancellationToken ct);
    }
}
