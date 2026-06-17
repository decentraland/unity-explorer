using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Minimap
{
    public interface ISceneRestrictionsView
    {
        RectTransform SceneRestrictionsIcon { get; set; }
        GameObject RestrictionTextPrefab { get; set; }
        GameObject ToastTextParent { get; set; }

        void HideRestrictionToast();

        UniTask CycleToastAsync();
    }

    public class SceneRestrictionsView : MonoBehaviour, ISceneRestrictionsView
    {
        [field: SerializeField]
        public RectTransform SceneRestrictionsIcon { get; set; }

        [field: SerializeField]
        public GameObject RestrictionTextPrefab { get; set; }

        [field: SerializeField]
        public CanvasGroup ToastCanvasGroup { get; set; }

        [field: SerializeField]
        public GameObject ToastTextParent { get; set; }

        [field: SerializeField]
        public float FadeTime { get; set; } = 0.3f;

        [field: SerializeField]
        public float CycleDurationTime { get; set; } = 3f;

        public RectTransform ToastRectTransform { get; set; }

        private CancellationTokenSource cts = new ();


        public void OnPointerEnter() => ShowRestrictionToast();
        public void OnPointerExit() => HideRestrictionToast();

        private void ShowRestrictionToast()
        {
            ToastCanvasGroup.gameObject.SetActive(true);
            ToastCanvasGroup.DOFade(1f, FadeTime);
        }

        public void HideRestrictionToast() =>
            ToastCanvasGroup.DOFade(0f, FadeTime).OnComplete(() => ToastCanvasGroup.gameObject.SetActive(false));

        public async UniTask CycleToastAsync()
        {
            if (ToastCanvasGroup.gameObject.activeInHierarchy) return;

            cts = cts.SafeRestart();

            ShowRestrictionToast();
            await UniTask.Delay(TimeSpan.FromSeconds(CycleDurationTime), cancellationToken: cts.Token).SuppressCancellationThrow();
            HideRestrictionToast();
        }

        private void Awake()
        {
            ToastCanvasGroup.alpha = 0;
            ToastCanvasGroup.gameObject.SetActive(false);
            SceneRestrictionsIcon.gameObject.SetActive(false);
            ToastRectTransform = ToastCanvasGroup.GetComponent<RectTransform>();
        }
    }
}
