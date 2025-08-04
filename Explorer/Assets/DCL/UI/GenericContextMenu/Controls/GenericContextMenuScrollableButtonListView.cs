using DCL.UI.GenericContextMenu.Controls.Configs;
using DCL.UI.Utilities;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DCL.UI.GenericContextMenu.Controls
{
    public class GenericContextMenuScrollableButtonListView : GenericContextMenuComponentBase
    {
        private const int BUTTON_HEIGHT = 36;

        [field: SerializeField] public VerticalLayoutGroup VerticalLayoutComponent { get; private set; }
        [field: SerializeField] public ScrollRect ScrollRect { get; private set; }
        [field: SerializeField] public Transform ScrollContentParent { get; private set; }

        private readonly List<GenericContextMenuSimpleButtonView> buttonViews = new ();

        public void Configure(ScrollableButtonListControlSettings settings, ControlsPoolManager controlsPoolManager)
        {
            ScrollRect.SetScrollSensitivityBasedOnPlatform();
            HorizontalLayoutComponent.padding = new RectOffset(0, 0, 0, 0);
            HorizontalLayoutComponent.spacing = 0;
            VerticalLayoutComponent.padding = settings.verticalLayoutPadding;
            VerticalLayoutComponent.spacing = settings.elementsSpacing;

            RectTransformComponent.sizeDelta = new Vector2(RectTransformComponent.sizeDelta.x, Math.Min(CalculateComponentHeight(settings), settings.maxHeight));

            int i = 0;
            foreach (string label in settings.dataLabels)
            {
                int index = i; // Capture the current index for the lambda expression
                buttonViews.Add((GenericContextMenuSimpleButtonView)controlsPoolManager.GetContextMenuComponent(
                    controlsPoolManager.GetSimpleButtonConfig(label, () => settings.callback.Invoke(index), settings.horizontalLayoutPadding, settings.horizontalLayoutSpacing),
                    i, ScrollContentParent));

                i++;
            }
        }

        private float CalculateComponentHeight(ScrollableButtonListControlSettings settings)
        {
            float totalHeight = (HorizontalLayoutComponent.padding.bottom * settings.dataLabels.Count)
                                + (HorizontalLayoutComponent.padding.top * settings.dataLabels.Count)
                                + VerticalLayoutComponent.padding.bottom
                                + VerticalLayoutComponent.padding.top
                                + (VerticalLayoutComponent.spacing * settings.dataLabels.Count)
                                + (BUTTON_HEIGHT * settings.dataLabels.Count);

            return totalHeight;
        }

        public override void UnregisterListeners()
        {
            buttonViews.Clear();
        }

        public override void RegisterCloseListener(Action listener)
        {
            foreach (GenericContextMenuSimpleButtonView button in buttonViews)
                button.ButtonComponent.onClick.AddListener(new UnityAction(listener));
        }
    }
}
