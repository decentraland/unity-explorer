using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.Emoji
{
    public class EmojiButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [field: SerializeField]
        public GameObject Tooltip { get; private set; }

        [field: SerializeField]
        public TMP_Text TooltipText { get; private set; }

        [field: SerializeField]
        public TMP_Text EmojiImage { get; private set; }

        [field: SerializeField]
        public Button Button { get; private set; }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if(!string.IsNullOrEmpty(TooltipText.text))
                Tooltip.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData) =>
            Tooltip.SetActive(false);
    }
}
