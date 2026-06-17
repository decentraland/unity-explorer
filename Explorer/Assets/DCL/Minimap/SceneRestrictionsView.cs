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

        UniTaskVoid CycleToastAsync();
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
        private float toastDeadline;
        private bool isCycling;


        public void OnPointerEnter() => ShowRestrictionToast();
        public void OnPointerExit() => HideRestrictionToast();

        private void ShowRestrictionToast()
        {
            ToastCanvasGroup.DOKill();
            ToastCanvasGroup.gameObject.SetActive(true);
            ToastCanvasGroup.DOFade(1f, FadeTime);
        }

        public void HideRestrictionToast()
        {
            ToastCanvasGroup.DOKill();
            cts.Cancel();
            ToastCanvasGroup.DOFade(0f, FadeTime).OnComplete(() => ToastCanvasGroup.gameObject.SetActive(false));
        }

        public async UniTaskVoid CycleToastAsync()
        {
            // Every caller pushes the hide time forward, so the toast stays visible as long as restrictions keep arriving.
            toastDeadline = UnityEngine.Time.realtimeSinceStartup + CycleDurationTime;

            if (isCycling) return;

            isCycling = true;
            cts = cts.SafeRestart();

            ShowRestrictionToast();

            while (UnityEngine.Time.realtimeSinceStartup < toastDeadline)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(toastDeadline - UnityEngine.Time.realtimeSinceStartup), cancellationToken: cts.Token).SuppressCancellationThrow();

                if (cts.IsCancellationRequested)
                    break;
            }

            isCycling = false;
            HideRestrictionToast();
        }

        private void Awake()
        {
            ToastCanvasGroup.alpha = 0;
            ToastCanvasGroup.gameObject.SetActive(false);
            SceneRestrictionsIcon.gameObject.SetActive(false);
            ToastRectTransform = ToastCanvasGroup.GetComponent<RectTransform>();
        }

        private void OnDestroy()
        {
            cts.SafeCancelAndDispose();
        }
    }
}
