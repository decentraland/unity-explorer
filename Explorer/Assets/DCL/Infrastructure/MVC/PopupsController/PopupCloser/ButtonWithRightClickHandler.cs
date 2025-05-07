using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MVC.PopupsController.PopupCloser
{
    public class ButtonWithRightClickHandler : MonoBehaviour, IPointerUpHandler
    {
        [field: SerializeField]
        public Button Button { get; private set; }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
                Button.onClick.Invoke();
        }

    }
}
