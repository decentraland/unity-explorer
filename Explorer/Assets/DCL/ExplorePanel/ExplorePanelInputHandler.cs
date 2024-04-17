using DCL.UI;
using MVC;
using System;
using UnityEngine.InputSystem;

namespace DCL.ExplorePanel
{
    public class ExplorePanelInputHandler : IDisposable
    {
        private readonly DCLInput dclInput;
        private readonly IMVCManager mvcManager;
        private static ExploreSections lastShownSection;

        public ExplorePanelInputHandler(DCLInput dclInput, IMVCManager mvcManager)
        {
            this.dclInput = dclInput;
            this.mvcManager = mvcManager;
            lastShownSection = ExploreSections.Navmap;
            RegisterHotkeys();
        }

        public void Dispose()
        {
            UnregisterHotkeys();
        }

        private void RegisterHotkeys()
        {
            dclInput.Shortcuts.MainMenu.performed += OnMainMenuHotkeyPressed;
            dclInput.Shortcuts.Map.performed += OnMapHotkeyPressed;
            dclInput.Shortcuts.Settings.performed += OnSettingsHotkeyPressed;
            dclInput.Shortcuts.Backpack.performed += OnBackpackHotkeyPressed;
        }

        private void UnregisterHotkeys()
        {
            dclInput.Shortcuts.MainMenu.performed -= OnMainMenuHotkeyPressed;
            dclInput.Shortcuts.Map.performed -= OnMapHotkeyPressed;
            dclInput.Shortcuts.Settings.performed -= OnSettingsHotkeyPressed;
            dclInput.Shortcuts.Backpack.performed -= OnBackpackHotkeyPressed;
        }

        private void OnMainMenuHotkeyPressed(InputAction.CallbackContext obj)
        {
            mvcManager.ShowAsync(ExplorePanelController.IssueCommand(new ExplorePanelParameter(lastShownSection)));
        }

        private void OnMapHotkeyPressed(InputAction.CallbackContext obj)
        {
            mvcManager.ShowAsync(ExplorePanelController.IssueCommand(new ExplorePanelParameter(ExploreSections.Navmap)));
            lastShownSection = ExploreSections.Navmap;
        }

        private void OnSettingsHotkeyPressed(InputAction.CallbackContext obj)
        {
            mvcManager.ShowAsync(ExplorePanelController.IssueCommand(new ExplorePanelParameter(ExploreSections.Settings)));
            lastShownSection = ExploreSections.Settings;
        }

        private void OnBackpackHotkeyPressed(InputAction.CallbackContext obj)
        {
            mvcManager.ShowAsync(ExplorePanelController.IssueCommand(new ExplorePanelParameter(ExploreSections.Backpack)));
            lastShownSection = ExploreSections.Backpack;
        }
    }
}
