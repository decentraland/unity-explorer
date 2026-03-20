using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.Chat
{
    public class ChatEntryReactionButtonHoverView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private static readonly Vector3 HOVERED_SCALE = new (1.2f, 1.2f, 1.2f);
        private const float ANIM_DURATION = 0.1f;

        [SerializeField] private Image icon;
        [SerializeField] private Sprite defaultSprite;
        [SerializeField] private Sprite hoveredSprite;
        [SerializeField] private Sprite clickedSprite;

        private bool isClicked;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (isClicked) return;

            icon.transform.DOScale(HOVERED_SCALE, ANIM_DURATION).SetEase(Ease.OutQuad);
            icon.sprite = hoveredSprite;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (isClicked) return;

            icon.transform.DOScale(Vector3.one, ANIM_DURATION).SetEase(Ease.OutQuad);
            icon.sprite = defaultSprite;
        }

        public void SetClicked(bool clicked)
        {
            isClicked = clicked;
            icon.sprite = clicked ? clickedSprite : defaultSprite;

            if (!clicked)
            {
                icon.transform.DOKill();
                icon.transform.localScale = Vector3.one;
            }
        }

        public void ResetState()
        {
            isClicked = false;
            icon.transform.DOKill();
            icon.transform.localScale = Vector3.one;
            icon.sprite = defaultSprite;
        }
    }
}
