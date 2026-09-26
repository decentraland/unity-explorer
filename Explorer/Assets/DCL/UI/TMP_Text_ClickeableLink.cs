using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.UI
{
    [RequireComponent(typeof(TMP_Text))]
    public class TMP_Text_ClickeableLink : MonoBehaviour, IPointerClickHandler
    {
        private TMP_Text text = null!;

        public event Action<string>? OnLinkClicked;

        private void Awake()
        {
            text = GetComponent<TMP_Text>();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // Detect if a link was clicked
            int linkIndex = TMP_TextUtilities.FindIntersectingLink(text, eventData.position, null);

            if (linkIndex == -1) return;
            TMP_LinkInfo linkInfo = text.textInfo.linkInfo[linkIndex];

            string url = linkInfo.GetLinkID();

            OnLinkClicked?.Invoke(url);
        }

        public void ClearHookedEvents() =>
            OnLinkClicked = null;
    }
}
