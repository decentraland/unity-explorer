using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using TMPro;
using UnityEngine;
using Utility;

namespace DCL.Chat.ChatMessages
{
    /// <summary>
    /// Inline toast shown when the user tries to add a reaction beyond the per-message limit.
    /// Visually matches the reaction tooltip style (dark rounded bubble, white text).
    /// Horizontally centered in the chat scroll view, vertically aligned to the reaction button.
    /// </summary>
    public class ReactionLimitToastView : MonoBehaviour
    {
        [SerializeField] private TMP_Text messageText = null!;
        [SerializeField] private CanvasGroup canvasGroup = null!;
        [SerializeField] private float displayDuration = 2f;
        [SerializeField] private Vector2 offset = new (0f, 40f);

        [Tooltip("Reference element whose center defines horizontal centering (e.g. the chat scroll view area).")]
        [SerializeField] private RectTransform? centeringReference;

        private CancellationTokenSource? showCts;

        private void Awake()
        {
            SetVisible(false);
        }

        public void Show(string message, RectTransform anchor)
        {
            showCts.SafeCancelAndDispose();
            showCts = new CancellationTokenSource();

            messageText.text = message;
            PositionAt(anchor);
            SetVisible(true);

            AutoHideAsync(showCts.Token).Forget();
        }

        public void Hide()
        {
            showCts.SafeCancelAndDispose();
            showCts = null;
            SetVisible(false);
        }

        private void PositionAt(RectTransform anchor)
        {
            var parent = (RectTransform)transform.parent;

            // Same conversion as ReactionPanelPositioner.PositionShortcutsBarAboveAnchor:
            // use anchor.position (pivot point), not GetWorldCorners.
            Vector3 localPos = parent.InverseTransformPoint(anchor.position);

            float localX = GetCenterX(parent) + offset.x;
            float localY = localPos.y + offset.y;

            ((RectTransform)transform).localPosition = new Vector3(localX, localY, 0f);
        }

        private float GetCenterX(RectTransform parentRect)
        {
            if (centeringReference == null)
                return parentRect.rect.center.x;

            Vector3 refCenter = centeringReference.TransformPoint(centeringReference.rect.center);
            return parentRect.InverseTransformPoint(refCenter).x;
        }

        private async UniTaskVoid AutoHideAsync(CancellationToken ct)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(displayDuration), cancellationToken: ct);
                if (!ct.IsCancellationRequested)
                    SetVisible(false);
            }
            catch (OperationCanceledException) { }
        }

        private void SetVisible(bool visible)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }

            gameObject.SetActive(visible);
        }

        private void OnDestroy()
        {
            showCts.SafeCancelAndDispose();
            showCts = null;
        }
    }
}
