﻿using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace MVC
{
    public abstract class ViewBase : MonoBehaviour
    {
        [field: SerializeField]
        protected Canvas canvas { get; private set; }

        [field: SerializeField]
        protected GraphicRaycaster raycaster { get; private set; }

        public void SetDrawOrder(CanvasOrdering order)
        {
            canvas.sortingLayerName = order.Layer.ToString();
            canvas.sortingOrder = order.OrderInLayer;
        }

        public virtual async UniTask ShowAsync(CancellationToken ct)
        {
            gameObject.SetActive(true);
            if (raycaster) raycaster.enabled = false; // Enable raycasts while the animation is playing
            await PlayShowAnimationAsync(ct);
            if (raycaster) raycaster.enabled = true;
        }

        public virtual async UniTask HideAsync(CancellationToken ct, bool isInstant = false)
        {
            if (raycaster) raycaster.enabled = false;

            if (!isInstant)
                await PlayHideAnimationAsync(ct);

            gameObject.SetActive(false);
        }

        protected virtual UniTask PlayShowAnimationAsync(CancellationToken ct) =>
            UniTask.CompletedTask;

        protected virtual UniTask PlayHideAnimationAsync(CancellationToken ct) =>
            UniTask.CompletedTask;

        public virtual void SetCanvasActive(bool isActive) =>
            canvas.enabled = isActive;
    }
}
