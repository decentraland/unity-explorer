using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI
{
    public class NameColorPickerView : MonoBehaviour
    {
        [field: Header("Toggle Button")]
        [field: SerializeField]
        public Button ToggleButton { get; private set; }

        [field: SerializeField]
        public ColorPickerView ColorPickerView { get; private set; }
    }
}
