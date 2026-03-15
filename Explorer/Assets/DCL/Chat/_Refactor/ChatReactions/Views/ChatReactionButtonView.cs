using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// View for the chat reaction button. The heart icon is set statically
    /// in the prefab and never changes at runtime.
    /// </summary>
    public class ChatReactionButtonView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private static readonly Vector3 HOVERED_SCALE = new (1.2f, 1.2f, 1.2f);
        private const float ANIM_DURATION = 0.1f;

        [field: SerializeField] public Button ReactionButton { get; private set; } = null!;
        [field: SerializeField] private Image icon { get; private set; } = null!;

        public void Show() => gameObject.SetActive(true);

        public void Hide() => gameObject.SetActive(false);

        public void OnPointerEnter(PointerEventData eventData)
        {
            icon.transform.DOScale(HOVERED_SCALE, ANIM_DURATION).SetEase(Ease.OutQuad);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            icon.transform.DOScale(Vector3.one, ANIM_DURATION).SetEase(Ease.OutQuad);
        }
    }
}
