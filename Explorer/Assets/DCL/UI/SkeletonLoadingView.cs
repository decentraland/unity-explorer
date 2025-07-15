using DG.Tweening;
using UnityEngine;

namespace DCL.UI
{
    public class SkeletonLoadingView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup loadedCanvasGroup = null!;
        [SerializeField] private CanvasGroup loadingCanvasGroup = null!;

        [SerializeField] private RectTransform[] bones = null!;
        [SerializeField] private float tweenDuration = 1f;
        [SerializeField] private float fadeDuration = 0.3f;

        private void Awake()
        {
            loadingCanvasGroup.interactable = false;
        }

        public void ShowLoading()
        {
            loadedCanvasGroup.alpha = 0f;
            loadedCanvasGroup.blocksRaycasts = false;
            loadedCanvasGroup.interactable = false;

            loadingCanvasGroup.alpha = 1f;
            loadingCanvasGroup.blocksRaycasts = true;

            foreach (var bone in bones)
            {
                bone.DOKill();
                bone.localPosition = new Vector3(0f, bone.localPosition.y, bone.localPosition.z);
                bone.DOLocalMoveX(bone.position.x + (bone.sizeDelta.x / 2f), tweenDuration)
                    .SetLoops(-1, LoopType.Restart);
            }
        }

        public void HideLoading()
        {
            loadingCanvasGroup.alpha = 0f;
            loadingCanvasGroup.blocksRaycasts = false;

            foreach (var bone in bones)
                bone.DOKill();

            loadedCanvasGroup.DOFade(1f, fadeDuration)
                             .OnComplete(() =>
                              {
                                  loadedCanvasGroup.blocksRaycasts = true;
                                  loadedCanvasGroup.interactable = true;
                              });
        }

        private void OnDisable() =>
            HideLoading();
    }
}
