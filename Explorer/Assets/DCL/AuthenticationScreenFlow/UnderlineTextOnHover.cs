using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.AuthenticationScreenFlow
{
    public class UnderlineTextOnHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private TMP_Text text;

        private FontStyles prevFontStyle;

        public void OnPointerEnter(PointerEventData eventData)
        {
            prevFontStyle = text.fontStyle;
            text.fontStyle = FontStyles.Underline;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            text.fontStyle = prevFontStyle;
        }
    }
}
