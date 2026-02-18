using Cysharp.Threading.Tasks;
using DCL.Settings.ModuleViews;
using Plugins.NativeWindowManager;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace DCL.Settings.ModuleControllers
{
    public class ResolutionSettingsController : SettingsFeatureController
    {
        private readonly SettingsDropdownModuleView view;
        private readonly List<Vector2Int> possibleResolutions = new ();

        public ResolutionSettingsController(SettingsDropdownModuleView view)
        {
            this.view = view;

            RefreshResolutionOptions(NativeWindowManager.FullScreenEnabled);

            view.DropdownView.Dropdown.onValueChanged.AddListener(SetResolutionSettingsOnChange);

            NativeWindowManager.CurrentResolutionChanged += OnCurrentResolutionChanged;
            NativeWindowManager.FullScreenChanged += OnFullScreenChanged;

            view.SetInteractable(!Application.isEditor && NativeWindowManager.FullScreenEnabled);
            OnCurrentResolutionChanged(NativeWindowManager.CurrentResolution);
        }

        private void OnFullScreenChanged(bool enabled)
        {
            SetViewInteractable(enabled);
            RefreshResolutionOptions(enabled);
        }

        private void OnCurrentResolutionChanged(Vector2Int resolution)
        {
            if (!NativeWindowManager.FullScreenEnabled)
            {
                view.DropdownView.Dropdown.options[0].text = FormatResolutionDropdownOption(resolution, false);
                view.DropdownView.Dropdown.RefreshShownValue();
            }
        }

        private void SetResolutionSettingsOnChange(int index)
        {
            NativeWindowManager.FullScreenResolution = possibleResolutions[index];
        }

        private void RefreshResolutionOptions(bool fullScreenEnabled)
        {
            view.DropdownView.Dropdown.options.Clear();

            if (fullScreenEnabled)
            {
                possibleResolutions.AddRange(NativeWindowManager.AvailableResolutions);
                var currentResolution = NativeWindowManager.FullScreenResolution;

                var currentResolutionIndex = -1;

                for (int i = 0; i < possibleResolutions.Count; i++)
                {
                    Vector2Int resolution = possibleResolutions[i];

                    if (resolution == currentResolution)
                        currentResolutionIndex = i;

                    view.DropdownView.Dropdown.options.Add(
                        new TMP_Dropdown.OptionData
                        {
                            text = FormatResolutionDropdownOption(resolution, true)
                        }
                    );
                }

                view.DropdownView.Dropdown.SetValueWithoutNotify(currentResolutionIndex);
            }
            else
            {
                view.DropdownView.Dropdown.options.Add(new TMP_Dropdown.OptionData { text = FormatResolutionDropdownOption(NativeWindowManager.CurrentResolution, false) });
            }

            view.DropdownView.Dropdown.RefreshShownValue();
        }

        private static string FormatResolutionDropdownOption(Vector2Int resolution, bool includeAspectRatio)
        {
            int width = resolution.x;
            int height = resolution.y;

            int tempWidth = width;
            int tempHeight = height;

            while (height != 0)
            {
                int rest = width % height;
                width = height;
                height = rest;
            }

            if (includeAspectRatio)
            {
                var aspectRationString = $"{tempWidth / width}:{tempHeight / width}";
                return $"{resolution.x}x{resolution.y} ({aspectRationString})";
            }

            return $"{resolution.x}x{resolution.y}";
        }

        public override void Dispose()
        {
            view.DropdownView.Dropdown.onValueChanged.RemoveListener(SetResolutionSettingsOnChange);
            NativeWindowManager.CurrentResolutionChanged -= OnCurrentResolutionChanged;
            NativeWindowManager.FullScreenChanged -= OnFullScreenChanged;
        }
    }
}
