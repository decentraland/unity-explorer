using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.CharacterCamera;
using DCL.InWorldCamera;
using DCL.UI;
using ECS.Abstract;
using MVC;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Utility.UIToolkit;
using Utility;

namespace DCL.Input.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(InputGroup))]
    public partial class UpdateShowHideUIInputSystem : BaseUnityLoopSystem
    {
        private enum UIVisibility
        {
            AllVisible,
            ExplorerHidden,
            AllHidden,
        }

        private const int TOAST_DURATION_MS = 3000;
        private const string EXPLORER_HIDDEN_TOAST = "Press   U   again to hide the scene UI.";
        private const string ALL_HIDDEN_TOAST = "Press   U   to show the UI again.";

        private readonly DCLInput dclInput;
        private readonly IMVCManager mvcManager;

        private SingleInstanceEntity camera;
        private CancellationTokenSource? toastCt;
        private readonly WarningNotificationView warningNotificationView;

        private UIVisibility visibility = UIVisibility.AllVisible;

        private readonly IReadOnlyList<InputAction> actionsThatEnableUI;

        private UpdateShowHideUIInputSystem(World world, IMVCManager mvcManager, WarningNotificationView warningNotificationView) : base(world)
        {
            dclInput = DCLInput.Instance;
            actionsThatEnableUI = new List<InputAction>
            {
                dclInput.Shortcuts.Communities,
                dclInput.Shortcuts.Map,
                dclInput.Shortcuts.Backpack,
                dclInput.Shortcuts.CameraReel,
                dclInput.Shortcuts.Settings,
                dclInput.Shortcuts.Controls,
                dclInput.Shortcuts.MainMenu,
                dclInput.UI.Submit,
            };

            this.mvcManager = mvcManager;
            this.warningNotificationView = warningNotificationView;
        }

        public override void Initialize()
        {
            camera = World.CacheCamera();
        }

        protected override void Update(float t)
        {
            // TODO: Should this really be in a system? It's not really updating anything, just checking for input triggers

            UIVisibility? fromInputs = CheckInputs();

            if (fromInputs.HasValue)
            {
                visibility = fromInputs.Value;

                // Common UIs
                mvcManager.SetAllViewsCanvasActive(visibility == UIVisibility.AllVisible);

                ShowOrHideToast();
            }
            else if (World.TryGet(camera, out ToggleUIRequest request))
            {
                visibility = request.Enable ? UIVisibility.AllVisible : UIVisibility.AllHidden;

                // Common UIs
                mvcManager.SetAllViewsCanvasActive(request.Except, visibility == UIVisibility.AllVisible);

                World.Remove<ToggleUIRequest>(camera);

                ShowOrHideToast();
            }

            foreach (UIDocumentTracker tracker in UIDocumentTracker.ActiveDocuments)
            {
                if (!tracker.CanBeHidden)
                    continue;

                bool visible = tracker.IsSceneUI
                    ? visibility != UIVisibility.AllHidden
                    : visibility == UIVisibility.AllVisible;

                tracker.Document.rootVisualElement.SetVisible(visible);
            }
        }

        private UIVisibility? CheckInputs()
        {
            // U cycles: all visible -> explorer hidden (scene UI kept) -> all hidden -> all visible
            if (dclInput.Shortcuts.ShowHideUI.WasPressedThisFrame())
                return visibility switch
                {
                    UIVisibility.AllVisible => UIVisibility.ExplorerHidden,
                    UIVisibility.ExplorerHidden => UIVisibility.AllHidden,
                    _ => UIVisibility.AllVisible,
                };

            foreach (InputAction inputAction in actionsThatEnableUI)
            {
                if (inputAction.WasPressedThisFrame())
                    return UIVisibility.AllVisible;
            }

            return null;
        }

        private void ShowOrHideToast()
        {
            if (visibility == UIVisibility.AllVisible)
            {
                toastCt.SafeCancelAndDispose();
                warningNotificationView.Hide();
                return;
            }

            string text = visibility == UIVisibility.ExplorerHidden ? EXPLORER_HIDDEN_TOAST : ALL_HIDDEN_TOAST;

            toastCt = toastCt.SafeRestart();

            warningNotificationView.AnimatedShowAsync(text, TOAST_DURATION_MS, toastCt.Token)
                                   .SuppressCancellationThrow()
                                   .Forget();
        }
    }
}
