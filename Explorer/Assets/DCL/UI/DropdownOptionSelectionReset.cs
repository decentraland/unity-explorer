using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.UI
{
    /// <summary>
    ///     Hands the EventSystem selection back to the owning dropdown when the pointer leaves an option row, so the
    ///     row's Selected tint ends with the hover instead of lingering on the last hovered option.
    /// </summary>
    public class DropdownOptionSelectionReset : MonoBehaviour, IPointerExitHandler
    {
        public void OnPointerExit(PointerEventData eventData)
        {
            EventSystem current = EventSystem.current;

            if (current == null || current.currentSelectedGameObject != gameObject)
                return;

            TMP_Dropdown? dropdown = GetComponentInParent<TMP_Dropdown>();
            current.SetSelectedGameObject(dropdown != null ? dropdown.gameObject : null);
        }
    }
}
