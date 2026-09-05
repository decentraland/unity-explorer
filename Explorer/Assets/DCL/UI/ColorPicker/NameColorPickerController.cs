using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.Profiles.Self;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.UI
{
    public class NameColorPickerController : IDisposable
    {
        public event Action<Color> OnColorChanged;
        public event Action OnColorPickerClosed;

        private readonly IMVCManager mvcManager;
        private readonly ISelfProfile selfProfile;
        private readonly ProfileChangesBus profileChangesBus;
        private readonly NameColorPickerView view;
        private readonly ColorPresetsSO colorPresets;
        private Color currentColor;
        private CancellationTokenSource saveCancellationToken;

        public Color CurrentColor => currentColor;

        public NameColorPickerController(
            IMVCManager mvcManager,
            ISelfProfile selfProfile,
            ProfileChangesBus profileChangesBus,
            NameColorPickerView view,
            ColorPresetsSO colorPresets)
        {
            this.mvcManager = mvcManager;
            this.selfProfile = selfProfile;
            this.profileChangesBus = profileChangesBus;
            this.view = view;
            this.colorPresets = colorPresets;

            view.ToggleButton.onClick.AddListener(TogglePanel);
        }

        public void Dispose()
        {
            saveCancellationToken?.SafeCancelAndDispose();
            view.ToggleButton.onClick.RemoveAllListeners();
            OnColorChanged = null;
        }

        public void SetColor(Color color) =>
            currentColor = color;

        private void TogglePanel() =>
            ShowColorPickerPopup();

        private void ShowColorPickerPopup()
        {
            // Get anchor position in world space
            Vector2 anchorPosition = view.ColorPickerAnchor != null ? view.ColorPickerAnchor.transform.position : Vector2.zero;

            var data = new ColorPickerPopupData
            {
                InitialColor = currentColor,
                ColorPresets = colorPresets.colors,
                Position = anchorPosition,
                EnableSaturationSlider = false,
                EnableValueSlider = false,
                OnColorChanged = (color) =>
                {
                    currentColor = color;
                    OnColorChanged.Invoke(color);
                }
            };

            mvcManager.ShowAsync(ColorPickerController.IssueCommand(data))
                      .ContinueWith(() =>
                       {
                           OnColorPickerClosed?.Invoke();
                       })
                      .Forget();
        }
    }
}
