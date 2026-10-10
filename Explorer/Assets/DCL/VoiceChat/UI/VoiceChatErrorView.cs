using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DG.Tweening;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.VoiceChat
{
    public class VoiceChatErrorView : MonoBehaviour
    {
        private const int HIDE_ERROR_PANEL_DELAY = 5000;

        [field: SerializeField]
        public CanvasGroup PanelCanvasGroup;

        private CancellationTokenSource cts = new ();

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
            try
            {
                await UniTask.Delay(HIDE_ERROR_PANEL_DELAY, cancellationToken: ct);
                await PanelCanvasGroup.DOFade(0, 0.5f).ToUniTask(cancellationToken: ct);
            }
            catch (OperationCanceledException) { PanelCanvasGroup.alpha = 0; }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"Error panel disable flow failed: {e.Message}");
                PanelCanvasGroup.alpha = 0;
            }
            finally
            {
                PanelCanvasGroup.interactable = false;
                PanelCanvasGroup.blocksRaycasts = false;
            }
        }
    }
}
