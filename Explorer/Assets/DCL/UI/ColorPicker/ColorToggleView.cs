using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI
{
    public class ColorToggleView : MonoBehaviour
    {
        [field: SerializeField]
        public Image ColorPicker { get; private set; }

        [field: SerializeField]
        public Image SelectionHighlight { get; private set; }

        [field: SerializeField]
        public Button Button { get; private set; }

        public event Action OnClicked;

        public void SetColor(Color c, bool on)
        {
            ColorPicker.color = c;
        }
    }
}
