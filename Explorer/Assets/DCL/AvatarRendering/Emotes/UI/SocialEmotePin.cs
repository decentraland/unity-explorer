using UnityEngine;
using DG.Tweening;

namespace DCL.AvatarRendering.Emotes
{
    public class SocialEmotePin : MonoBehaviour
    {
        [SerializeField]
        private RectTransform arrow;

        [SerializeField]
        private float arrowDisplacementLength = 2.0f;

        [SerializeField]
        private float arrowAnimationDuration = 0.2f;

        private void OnEnable()
        {
            arrow.DOLocalMove(new Vector3(0.0f, -arrowDisplacementLength, 0.0f), arrowAnimationDuration).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutQuad);
        }

        private void OnDisable()
        {
            arrow.DOKill();
        }
    }
}
