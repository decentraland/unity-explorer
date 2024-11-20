using DCL.ExplorePanel.Components;
using DCL.UI;
using MVC;
using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace DCL.ExplorePanel
{
    public class ExplorePanelInputHandler : IDisposable, IExplorePanelEscapeAction
    {
        private readonly DCLInput dclInput;
        private readonly IMVCManager mvcManager;
        private readonly LinkedList<Action<InputAction.CallbackContext>> escapeActions = new ();
        private readonly bool includeCameraReel;
        private static ExploreSections lastShownSection;

        public ExplorePanelInputHandler(DCLInput dclInput, IMVCManager mvcManager, bool includeCameraReel)
        {
            this.dclInput = dclInput;
            this.mvcManager = mvcManager;
            this.includeCameraReel = includeCameraReel;
            lastShownSection = ExploreSections.Navmap;
            RegisterHotkeys();
        }

        public void RegisterEscapeAction(Action<InputAction.CallbackContext> action)
        {
            if (escapeActions.Count > 0)
                dclInput.UI.Close.performed -= escapeActions.Last.Value;

            dclInput.UI.Close.performed += action;

            if (!escapeActions.Contains(action))
                escapeActions.AddLast(action);
        }

        public void RemoveEscapeAction(Action<InputAction.CallbackContext> action)
        {
            if (!escapeActions.Contains(action)) return;

            dclInput.UI.Close.performed -= action;
            escapeActions.Remove(action);

            if (escapeActions.Count > 0)
                dclInput.UI.Close.performed += escapeActions.Last.Value;
        }

        public void Dispose() =>
            UnregisterHotkeys();

        private void RegisterHotkeys()
        {
            dclInput.Shortcuts.MainMenu.performed += OnMainMenuHotkeyPressed;
            dclInput.Shortcuts.Map.performed += OnMapHotkeyPressed;
            dclInput.Shortcuts.Settings.performed += OnSettingsHotkeyPressed;
            dclInput.Shortcuts.Backpack.performed += OnBackpackHotkeyPressed;
            dclInput.Shortcuts.CameraReel.performed += OnCameraReelHotkeyPressed;
        }

        private void UnregisterHotkeys()
        {
            dclInput.Shortcuts.MainMenu.performed -= OnMainMenuHotkeyPressed;
            dclInput.Shortcuts.Map.performed -= OnMapHotkeyPressed;
            dclInput.Shortcuts.Settings.performed -= OnSettingsHotkeyPressed;
            dclInput.Shortcuts.Backpack.performed -= OnBackpackHotkeyPressed;
            dclInput.Shortcuts.CameraReel.performed -= OnCameraReelHotkeyPressed;

            foreach (var escapeAction in escapeActions)
                dclInput.UI.Close.performed -= escapeAction;
            escapeActions.Clear();
        }

        private void OnMainMenuHotkeyPressed(InputAction.CallbackContext obj) =>
            mvcManager.ShowAsync(ExplorePanelController.IssueCommand(new ExplorePanelParameter(lastShownSection)));

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

        private void OnCameraReelHotkeyPressed(InputAction.CallbackContext obj)
        {
            if (!includeCameraReel) return;

            mvcManager.ShowAsync(ExplorePanelController.IssueCommand(new ExplorePanelParameter(ExploreSections.CameraReel)));
            lastShownSection = ExploreSections.CameraReel;
        }
    }
}
