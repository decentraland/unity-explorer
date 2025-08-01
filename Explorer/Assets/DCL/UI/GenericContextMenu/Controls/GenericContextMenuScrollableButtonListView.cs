using DCL.UI.GenericContextMenu.Controls.Configs;
using DCL.UI.Utilities;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.GenericContextMenu.Controls
{
    public class GenericContextMenuScrollableButtonListView : GenericContextMenuComponentBase
    {
        [field: SerializeField] public HorizontalLayoutGroup VerticalLayoutComponent { get; private set; }
        [field: SerializeField] public ScrollRect ScrollRect { get; private set; }
        [field: SerializeField] public Transform ScrollContentParent { get; private set; }

        private void Awake()
        {
            ScrollRect.SetScrollSensitivityBasedOnPlatform();
        }

        public void Configure(ScrollableButtonListControlSettings settings, ControlsPoolManager controlsPoolManager)
        {
            HorizontalLayoutComponent.padding = new RectOffset(0, 0, 0, 0);
            HorizontalLayoutComponent.spacing = 0;
            VerticalLayoutComponent.padding = settings.verticalLayoutPadding;
            VerticalLayoutComponent.spacing = settings.elementsSpacing;

            RectTransformComponent.sizeDelta = new Vector2(RectTransformComponent.sizeDelta.x, Math.Min(CalculateComponentHeight(settings), settings.maxHeight));

            for(int i = 0; i < settings.dataLabels.Length; i++)
                controlsPoolManager.GetContextMenuComponent(
                    new SimpleButtonContextMenuControlSettings(settings.dataLabels[i], () => settings.callback.Invoke(i), settings.horizontalLayoutPadding, settings.horizontalLayoutSpacing),
                    i, ScrollContentParent);
        }

        private float CalculateComponentHeight(ScrollableButtonListControlSettings settings)
        {
            float totalHeight = (HorizontalLayoutComponent.padding.bottom * settings.dataLabels.Length)
                                + (HorizontalLayoutComponent.padding.top * settings.dataLabels.Length)
                                + VerticalLayoutComponent.padding.bottom
                                + VerticalLayoutComponent.padding.top
                                + (VerticalLayoutComponent.spacing * settings.dataLabels.Length);

            return totalHeight;
        }

        public override void UnregisterListeners()
        {
        }

        public override void RegisterCloseListener(Action listener)
        {
        }
    }
}
