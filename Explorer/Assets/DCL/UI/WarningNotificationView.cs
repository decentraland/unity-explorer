using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DG.Tweening;
using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI
{
    public class WarningNotificationView : MonoBehaviour
    {
        private const float FADE_DURATION = 0.3f;

        [field: SerializeField] public CanvasGroup CanvasGroup { get; private set; }
        [field: SerializeField] public TMP_Text Text { get; private set; }
        [field: SerializeField] public Button CloseButton { get; private set; }

        public bool WasEverClosed { get; private set; }

        // Toasts that live in an always-loaded canvas are kept inactive until shown, so the close button has to put
        // the object back the way it found it
        private bool togglesGameObject;

        private void Awake() =>
            CloseButton.onClick.AddListener(() =>
            {
                WasEverClosed = true;
                Hide();
            });

        public void SetText(string text) =>
            Text.text = text;

        public void Show(CancellationToken ct = default, bool toggleGameObject = false)
        {
            if (!CanvasGroup) return;

            togglesGameObject = toggleGameObject;

            if (toggleGameObject)
            {
                CanvasGroup.alpha = 0;
                gameObject.SetActive(true);
            }

            CanvasGroup.DOFade(1, FADE_DURATION).ToUniTask(cancellationToken: ct);
            CanvasGroup.interactable = true;
            CanvasGroup.blocksRaycasts = true;
        }

        public void Hide(bool instant = false, CancellationToken ct = default)
        {
            if (!CanvasGroup) return;

            CanvasGroup.interactable = false;
            CanvasGroup.blocksRaycasts = false;

            if (instant)
            {
                CanvasGroup.alpha = 0;
                DeactivateIfToggled();
                return;
            }

            FadeOutAsync(ct).Forget();
        }

        public async UniTask AnimatedShowAsync(int showDurationMs, CancellationToken ct = default, bool toggleGameObject = false)
        {
            Show(ct, toggleGameObject);
            await UniTask.Delay(showDurationMs, cancellationToken: ct);
            Hide(ct: ct);
        }

        public async UniTask AnimatedShowAsync(string text, int showDurationMs, CancellationToken ct = default, bool toggleGameObject = false)
        {
            SetText(text);
            Show(ct, toggleGameObject);
            await UniTask.Delay(showDurationMs, cancellationToken: ct);
            Hide(ct: ct);
        }

        private async UniTaskVoid FadeOutAsync(CancellationToken ct)
        {
            try
            {
                await CanvasGroup.DOFade(0, FADE_DURATION).ToUniTask(cancellationToken: ct);
                DeactivateIfToggled();
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, new ReportData(ReportCategory.UI)); }
        }

        private void DeactivateIfToggled()
        {
            if (togglesGameObject)
                gameObject.SetActive(false);
        }
    }
}
