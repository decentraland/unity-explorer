﻿using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace MVC
{
    public abstract class ViewBase : MonoBehaviour
    {
        public event Action? OnViewHidden;
        public event Action? OnViewShown;

        [field: Header("Can be Null")]
        [field: SerializeField] protected Canvas? canvas { get; private set; }
        [field: SerializeField] protected GraphicRaycaster? raycaster { get; private set; }
        public void SetDrawOrder(CanvasOrdering order)
        {
            if (canvas != null)
            {
                canvas.sortingLayerName = order.Layer.ToString();
                canvas.sortingOrder = order.OrderInLayer;
            }
        }

        public virtual async UniTask ShowAsync(CancellationToken ct)
        {
            gameObject.SetActive(true);
            if (raycaster != null) raycaster.enabled = false; // Disable raycasts while the show animation is playing
            await PlayShowAnimationAsync(ct);
            if (raycaster != null) raycaster.enabled = true;
            OnViewShown?.Invoke();
        }

        public virtual async UniTask HideAsync(CancellationToken ct, bool isInstant = false)
        {
            if (raycaster != null) raycaster.enabled = false;

            if (!isInstant)
                await PlayHideAnimationAsync(ct);

            gameObject.SetActive(false);
            OnViewHidden?.Invoke();
        }

        protected virtual UniTask PlayShowAnimationAsync(CancellationToken ct) =>
            UniTask.CompletedTask;

        protected virtual UniTask PlayHideAnimationAsync(CancellationToken ct) =>
            UniTask.CompletedTask;

        public virtual void SetCanvasActive(bool isActive)
        {
            if (canvas != null) { canvas.enabled = isActive; }
        }
    }
}
