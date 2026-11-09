using DCL.Input;
using MVC;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.UI
{
    /// <summary>
    ///     Swaps the cursor for the interaction one while hovered, so clickable areas that are not standard buttons read as clickable.
    /// </summary>
    public class InteractionCursorOnHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public void OnPointerEnter(PointerEventData _) =>
            ViewDependencies.Cursor.SetStyle(CursorStyle.Interaction, true);

        public void OnPointerExit(PointerEventData _) =>
            ViewDependencies.Cursor.SetStyle(CursorStyle.Normal);

        // Nothing exits a panel that closes under the pointer, so the forced style is dropped here as well
        private void OnDisable() =>
            ViewDependencies.Cursor.SetStyle(CursorStyle.Normal);
    }
}
