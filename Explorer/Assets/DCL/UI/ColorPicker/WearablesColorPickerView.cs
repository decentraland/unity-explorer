using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI
{
    public class WearablesColorPickerView : MonoBehaviour
    {
        [field: Header("Toggle Button")]
        [field: SerializeField]
        public Button ToggleButton { get; private set; }

        /// <summary>
        /// The "COLOR" text GameObject child under ToggleButton
        /// </summary>
        [field: SerializeField]
        public GameObject ToggleText { get; private set; }

        [field: SerializeField]
        public bool ShowToggleText { get; set; } = true;

        [field: SerializeField]
        public Image ColorPreviewImage { get; private set; }

        [field: SerializeField]
        public GameObject ArrowUpMark { get; private set; }

        [field: SerializeField]
        public GameObject ArrowDownMark { get; private set; }

        [field: Header("ColorPicker")]
        [field: SerializeField]
        public ColorPickerView ColorPickerView { get; private set; }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (ToggleText != null)
                ToggleText.SetActive(ShowToggleText);
        }
#endif
    }
}
