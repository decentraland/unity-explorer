using DCL.Prefs;
using UnityEngine;
using UnityEngine.UIElements;
using Utility.UIToolkit;

namespace DCL.DebugUtilities.Views
{
    /// <summary>
    ///     Container with the scroll view for all possible debug utilities
    /// </summary>
    [UxmlElement]
    public partial class DebugContainer : VisualElement
    {
        private const string MAXIMIZED_CLASS = "debug-panel--maximized";
        private const string WIDE_CLASS = "debug-panel--wide";
        private const float MAX_WIDTH_SCREEN_FRACTION = 0.7f;

        internal VisualElement containerRoot => this.Q<VisualElement>("Parent");
        private VisualElement mainPanel;
        private VisualElement resizeHandle;
        private Button toolButton;
        private Button maximizeButton;

        private bool isResizing;

        // Resolved from the USS rule on first layout after the maximized class is applied.
        // Falls back to 0 until captured; consumers gate on > 0.
        private float baselineMaximizedWidth;
        private bool restoreSavedWidthOnNextLayout;

        public bool IsMaximized { get; private set; }

        internal void Initialize()
        {
            toolButton = this.Q<Button>("OpenPanelButton");
            mainPanel = this.Q("Panel");

            Button closeButton = this.Q<Button>("CloseButton");
            closeButton.clicked += () => mainPanel.SetDisplayed(false);
            toolButton.clicked += TogglePanelVisibility;

            maximizeButton = this.Q<Button>("MaximizeButton");
            if (maximizeButton != null)
                maximizeButton.clicked += ToggleMaximized;

            resizeHandle = this.Q<VisualElement>("ResizeHandle");
            if (resizeHandle != null)
            {
                resizeHandle.RegisterCallback<PointerDownEvent>(OnHandlePointerDown);
                resizeHandle.RegisterCallback<PointerMoveEvent>(OnHandlePointerMove);
                resizeHandle.RegisterCallback<PointerUpEvent>(OnHandlePointerUp);
            }

            mainPanel.RegisterCallback<GeometryChangedEvent>(OnPanelGeometryChanged);

            if (DCLPlayerPrefs.GetBool(DCLPrefKeys.DEBUG_PANEL_MAXIMIZED))
                SetMaximized(true);
        }

        public void SetPanelVisibility(bool newVisibility) =>
            mainPanel.SetDisplayed(newVisibility);

        public void TogglePanelVisibility() =>
            mainPanel.SetDisplayed(!IsPanelVisible());

        public bool IsPanelVisible() =>
            mainPanel.style.display == DisplayStyle.Flex;

        public void HideToggleButton() =>
            toolButton.style.display = DisplayStyle.None;

        public void SetMaximized(bool maximized)
        {
            if (IsMaximized == maximized) return;
            IsMaximized = maximized;

            if (maximized)
            {
                mainPanel.AddToClassList(MAXIMIZED_CLASS);

                if (baselineMaximizedWidth > 0f)
                    ApplyWidth(ClampWidth(LoadSavedWidth()));
                else
                    // Baseline isn't known yet; defer width application until the first layout pass.
                    restoreSavedWidthOnNextLayout = true;
            }
            else
            {
                mainPanel.RemoveFromClassList(MAXIMIZED_CLASS);
                mainPanel.RemoveFromClassList(WIDE_CLASS);
                mainPanel.style.width = StyleKeyword.Null;
            }

            DCLPlayerPrefs.SetBool(DCLPrefKeys.DEBUG_PANEL_MAXIMIZED, maximized, save: true);
        }

        public void ToggleMaximized() =>
            SetMaximized(!IsMaximized);

        private void OnPanelGeometryChanged(GeometryChangedEvent evt)
        {
            // Capture the USS-defined maximized width once the maximized class is laid out.
            // Skip if user has already overridden via inline style (post-drag) — only the very first
            // layout reflects the pure USS rule.
            if (baselineMaximizedWidth <= 0f
                && IsMaximized
                && mainPanel.style.width.keyword == StyleKeyword.Null)
            {
                float resolved = mainPanel.resolvedStyle.width;
                if (resolved > 0f && float.IsFinite(resolved))
                    baselineMaximizedWidth = resolved;
            }

            if (restoreSavedWidthOnNextLayout && baselineMaximizedWidth > 0f)
            {
                restoreSavedWidthOnNextLayout = false;
                ApplyWidth(ClampWidth(LoadSavedWidth()));
            }
        }

        private float LoadSavedWidth() =>
            DCLPlayerPrefs.GetFloat(DCLPrefKeys.DEBUG_PANEL_MAXIMIZED_WIDTH, baselineMaximizedWidth);

        private void OnHandlePointerDown(PointerDownEvent evt)
        {
            if (!IsMaximized) return;

            isResizing = true;
            resizeHandle.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnHandlePointerMove(PointerMoveEvent evt)
        {
            if (!isResizing) return;

            // Per-frame delta is immune to the panel-coord drift that affects evt.position
            // once the panel itself has been resized during the drag. The panel is anchored to
            // the right edge, so a leftward pointer delta (negative dx) increases the width.
            float newWidth = ClampWidth(mainPanel.resolvedStyle.width - evt.deltaPosition.x);
            ApplyWidth(newWidth);
        }

        private void OnHandlePointerUp(PointerUpEvent evt)
        {
            if (!isResizing) return;

            isResizing = false;
            resizeHandle.ReleasePointer(evt.pointerId);
            DCLPlayerPrefs.SetFloat(DCLPrefKeys.DEBUG_PANEL_MAXIMIZED_WIDTH, mainPanel.resolvedStyle.width, save: true);
        }

        // 2-column kicks in when the panel is at least 2× the baseline maximized width.
        private const float WIDE_LAYOUT_MULTIPLIER = 2f;
        private const float WIDE_LAYOUT_HYSTERESIS = 30f;

        private void ApplyWidth(float width)
        {
            mainPanel.style.width = width;

            if (baselineMaximizedWidth <= 0f) return;

            float threshold = baselineMaximizedWidth * WIDE_LAYOUT_MULTIPLIER;
            bool currentlyWide = mainPanel.ClassListContains(WIDE_CLASS);

            // Hysteresis avoids toggling on every pixel of drag right at the boundary.
            if (!currentlyWide && width > threshold)
                mainPanel.AddToClassList(WIDE_CLASS);
            else if (currentlyWide && width < threshold - WIDE_LAYOUT_HYSTERESIS)
                mainPanel.RemoveFromClassList(WIDE_CLASS);
        }

        private float ClampWidth(float width)
        {
            float min = baselineMaximizedWidth > 0f ? baselineMaximizedWidth : width;
            return Mathf.Clamp(width, min, Screen.width * MAX_WIDTH_SCREEN_FRACTION);
        }
    }
}
