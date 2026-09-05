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
        private const int TOAST_DURATION_MS = 3000;

        private readonly DCLInput dclInput;
        private readonly IMVCManager mvcManager;

        private SingleInstanceEntity camera;
        private CancellationTokenSource? toastCt;
        private readonly WarningNotificationView warningNotificationView;

        private bool currentUIVisibilityState = true;

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

            bool? fromInputs = CheckInputs();

            if (fromInputs.HasValue)
            {
                currentUIVisibilityState = fromInputs.Value;

                // Common UIs
                mvcManager.SetAllViewsCanvasActive(currentUIVisibilityState);

                ShowOrHideToast();
            }
            else if (World.TryGet(camera, out ToggleUIRequest request))
            {
                currentUIVisibilityState = request.Enable;

                // Common UIs
                mvcManager.SetAllViewsCanvasActive(request.Except, currentUIVisibilityState);

                World.Remove<ToggleUIRequest>(camera);

                ShowOrHideToast();
            }

            foreach (UIDocumentTracker tracker in UIDocumentTracker.ActiveDocuments)
            {
                if(!tracker.CanBeHidden)
                    continue;
                tracker.Document.rootVisualElement.SetVisible(currentUIVisibilityState);
            }
        }

        private bool? CheckInputs()
        {
            if (dclInput.Shortcuts.ShowHideUI.WasPressedThisFrame())
                return !currentUIVisibilityState;

            foreach (InputAction inputAction in actionsThatEnableUI)
            {
                if (inputAction.WasPressedThisFrame())
                    return true;
            }

            return null;
        }

        private void ShowOrHideToast()
        {
            if (!currentUIVisibilityState)
            {
                toastCt = toastCt.SafeRestart();

                warningNotificationView.AnimatedShowAsync(TOAST_DURATION_MS, toastCt.Token)
                                       .SuppressCancellationThrow()
                                       .Forget();
            }
            else
            {
                toastCt.SafeCancelAndDispose();
                warningNotificationView.Hide();
            }
        }
    }
}
