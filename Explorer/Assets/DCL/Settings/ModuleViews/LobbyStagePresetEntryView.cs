using DCL.UI;
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.Settings.ModuleViews
{
    /// <summary>
    ///     One selectable backdrop thumbnail in the lobby background picker.
    /// </summary>
    public class LobbyStagePresetEntryView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Action<bool>? HoverChanged;

        private bool hovered;

        [field: SerializeField] public Button Button { get; private set; } = null!;
        [field: SerializeField] public RawImage Thumbnail { get; private set; } = null!;
        [field: SerializeField] public AspectRatioFitter ThumbnailFitter { get; private set; } = null!;
        [field: SerializeField] public GameObject SelectedOutline { get; private set; } = null!;
        [field: SerializeField] public GameObject Checkmark { get; private set; } = null!;
        [field: SerializeField] public HoverableTooltip Tooltip { get; private set; } = null!;

        public void SetSelected(bool selected)
        {
            SelectedOutline.SetActive(selected);
            Checkmark.SetActive(selected);
        }

        public void SetTexture(Texture? texture) =>
            CoverFit.Apply(Thumbnail, ThumbnailFitter, texture);

        public void OnPointerEnter(PointerEventData _) =>
            SetHovered(true);

        public void OnPointerExit(PointerEventData _) =>
            SetHovered(false);

        // No exit event arrives for a pointer still over the entry when its panel closes
        private void OnDisable()
        {
            SetHovered(false);
        }

        private void SetHovered(bool value)
        {
            if (hovered == value) return;

            hovered = value;
            HoverChanged?.Invoke(value);
        }
    }
}
