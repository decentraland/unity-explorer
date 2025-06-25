using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.VoiceChat
{
    public class VoiceChatErrorView : MonoBehaviour
    {
        [field: SerializeField]
        public CanvasGroup PanelCanvasGroup;

        private CancellationTokenSource cts;

        private void Start()
        {
            cts = new CancellationTokenSource();
        }

        public void SetActive(bool isActive)
        {
            gameObject.SetActive(isActive);
            PanelCanvasGroup.alpha = isActive ? 1 : 0;
            PanelCanvasGroup.interactable = isActive;
            PanelCanvasGroup.blocksRaycasts = isActive;
        }

        public void StartErrorPanelDisableFlow()
        {
            cts = cts.SafeRestart();
            StartErrorPanelDisableFlowAsync(cts.Token).Forget();
        }

        private async UniTaskVoid StartErrorPanelDisableFlowAsync(CancellationToken ct)
        {
            await UniTask.Delay(5000, cancellationToken: ct);
            PanelCanvasGroup.DOFade(0, 0.5f).OnComplete(() =>
            {
                PanelCanvasGroup.interactable = false;
                PanelCanvasGroup.blocksRaycasts = false;
            });
        }
    }
}
