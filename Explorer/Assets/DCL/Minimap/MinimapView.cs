using DCL.MapRenderer.ConsumerUtils;
using DCL.MapRenderer.MapLayers.Pins;
using DCL.UI;
using DCL.UI.Buttons;
using MVC;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Minimap
{
    public class MinimapView : ViewBase, IView
    {
        private const float INFO_SECTION_HEIGHT = 77f;
        private const float INFO_SECTION_EXPANDED_HEIGHT = 117f;
        private const float SKYBOX_SECTION_HEIGHT = 28f;
        private const float SKYBOX_SECTION_EXPANDED_HEIGHT = 60f;

        [field: SerializeField]
        internal RawImage mapRendererTargetImage { get; private set; } = null!;

        [field: SerializeField]
        internal HoverableButton minimapRendererButton { get; private set; } = null!;

        [field: SerializeField]
        internal PixelPerfectMapRendererTextureProvider pixelPerfectMapRendererTextureProvider { get; private set; } = null!;

        [field: SerializeField]
        internal int mapRendererVisibleParcels { get; private set; }

        [field: SerializeField]
        internal Button expandMinimapButton { get; private set; } = null!;

        [field: SerializeField]
        internal Button collapseMinimapButton { get; private set; } = null!;

        [field: SerializeField]
        internal TMP_Text placeNameText { get; private set; } = null!;

        [field: SerializeField]
        internal TMP_Text placeCoordinatesText { get; private set; } = null!;

        [field: SerializeField]
        internal RectTransform sdk6Label { get; private set; } = null!;

        [field: SerializeField]
        internal RectTransform minimapContainer { get; private set; } = null!;

        [field: SerializeField]
        internal GameObject mapRoot { get; private set; } = null!;

        [field: SerializeField]
        internal SceneRestrictionsView sceneRestrictionsView { get; private set; } = null!;

        [field: SerializeField]
        internal ButtonView minimapContextualButtonView { get; private set; } = null!;

        [field: SerializeField]
        internal ToggleButtonWithDisabledState favoriteButton { get; private set; } = null!;

        [field: SerializeField]
        internal Button donateButton { get; private set; } = null!;

        [field: SerializeField]
        internal MinimapPinMarkerObject destinationPinMarker { get; private set; } = null!;

        [field: SerializeField]
        internal GameObject ownPlayerBannedMark { get; private set; } = null!;

        [field: SerializeField]
        internal GameObject ownPlayerBannedTooltip { get; private set; } = null!;

        [SerializeField] internal Button contextMenuButton = null!;

        [field: Header("Sections")]
        [field: SerializeField]
        internal RectTransform infoSection { get; private set; } = null!;

        [field: SerializeField]
        internal GameObject worldsSection { get; private set; } = null!;

        [field: SerializeField]
        internal RectTransform skyboxConfigSection { get; private set; } = null!;

        [field: SerializeField]
        internal GameObject skyboxWidget { get; private set; } = null!;

        [field: SerializeField]
        internal SimpleHoverableButton skyboxButton { get; private set; } = null!;

        [field: SerializeField]
        internal TMP_Text onlineUsersText { get; private set; } = null!;

        [field: SerializeField]
        internal Toggle timeProgressionToggle { get; private set; } = null!;

        [field: SerializeField]
        internal Slider timeSlider { get; private set; } = null!;

        [field: SerializeField]
        internal TMP_Text timeText { get; private set; } = null!;

        [field: SerializeField]
        internal CanvasGroup skyboxCanvasGroup { get; private set; } = null!;

        internal void SetWorldsSectionVisible(bool isVisible)
        {
            worldsSection.SetActive(isVisible);
            SetHeight(infoSection, isVisible ? INFO_SECTION_EXPANDED_HEIGHT : INFO_SECTION_HEIGHT);
        }

        internal void SetMapResizingAllowed(bool isAllowed)
        {
            expandMinimapButton.interactable = isAllowed;
            collapseMinimapButton.interactable = isAllowed;
        }

        internal void SetSkyboxWidgetVisible(bool isVisible)
        {
            skyboxWidget.SetActive(isVisible);
            SetHeight(skyboxConfigSection, isVisible ? SKYBOX_SECTION_EXPANDED_HEIGHT : SKYBOX_SECTION_HEIGHT);
        }

        private static void SetHeight(RectTransform target, float height)
        {
            Vector2 size = target.sizeDelta;
            size.y = height;
            target.sizeDelta = size;
        }
    }
}
