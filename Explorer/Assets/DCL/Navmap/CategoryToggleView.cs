using DCL.MapRenderer.MapLayers;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Navmap
{
    public class CategoryToggleView : MonoBehaviour
    {
        public event Action<MapLayer, bool, CategoryToggleView> ToggleChanged;

        [field: SerializeField]
        public MapLayer Layer { get; private set; }

        [field: SerializeField]
        public Toggle Toggle { get; private set; }

        [field: SerializeField]
        public TMP_Text Label { get; private set; }

        [field: SerializeField]
        public Image Background { get; private set; }

        [field: SerializeField]
        public Color OnColor { get; private set; }

        [field: SerializeField]
        public Color OffColor { get; private set; }

        private void Start()
        {
            Toggle.onValueChanged.AddListener(OnToggleValueChanged);
        }

        public void SetVisualStatus(bool isOn)
        {
            Label.color = isOn ? OffColor : OnColor;
            Background.color = isOn ? OnColor : OffColor;
        }

        private void OnToggleValueChanged(bool isOn)
        {
            SetVisualStatus(isOn);
            ToggleChanged?.Invoke(Layer, isOn, this);
        }
    }
}
