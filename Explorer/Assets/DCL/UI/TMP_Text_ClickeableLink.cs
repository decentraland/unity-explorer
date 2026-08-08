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

            ActivateLink(linkInfo.GetLinkID());
        }

        public void ClearHookedEvents() =>
            OnLinkClicked = null;

        /// <summary>
        ///     Hands an activated link's id to the subscribers. <see cref="OnPointerClick" /> calls it once the pointer
        ///     has been hit-tested against the laid-out text; kept separate — and visible to the EditMode tests — so
        ///     what a subscriber does with a link can be covered without a rendered canvas to click on.
        /// </summary>
        internal void ActivateLink(string linkId) =>
            OnLinkClicked?.Invoke(linkId);
    }
}
