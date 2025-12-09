using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.AuthenticationScreenFlow
{
    public class UnderlineTextOnHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private TMP_Text text;

        public void OnPointerEnter(PointerEventData eventData)
        {
            text.SetText($"<u>{text.text}</u>");
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            text.SetText(text.text.Replace("<u>", "").Replace("</u>", ""));
        }
    }
}
