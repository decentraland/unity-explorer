using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace DCL.Navmap
{
    public class FloatingPanelController
    {
        private readonly FloatingPanelView view;

        public FloatingPanelController(FloatingPanelView view)
        {
            this.view = view;
            view.closeButton.onClick.RemoveAllListeners();
            view.closeButton.onClick.AddListener(HidePanel);
            view.gameObject.SetActive(false);
        }

        public void ShowPanel()
        {
            view.rectTransform.localScale = Vector3.zero;
            view.gameObject.SetActive(true);
            view.rectTransform.DOScale(Vector3.one, 0.5f).SetEase(Ease.InCirc);
        }

        private void HidePanel()
        {
            view.rectTransform.localScale = Vector3.one;
            view.rectTransform.DOScale(Vector3.zero, 0.5f).SetEase(Ease.OutCirc).OnComplete(()=>view.gameObject.SetActive(false));
        }

        private async UniTaskVoid AnimatePanelShow()
        {
        }
    }
}
