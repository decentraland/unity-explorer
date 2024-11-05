using DCL.MapRenderer.MapLayers;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Navmap
{
    public class CategoryToggleView : MonoBehaviour
    {
        public event Action<MapLayer, bool> ToggleChanged;

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

        private void OnToggleValueChanged(bool isOn)
        {
            Label.color = isOn ? OnColor : OffColor;
            Background.color = isOn ? OffColor : OnColor;
            ToggleChanged?.Invoke(Layer, isOn);
        }
    }
}
