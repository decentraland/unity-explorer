using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.CharacterCamera;
using DCL.DebugUtilities;
using DCL.InWorldCamera;
using DCL.UI;
using ECS.Abstract;
using MVC;
using UnityEngine.UIElements;
using Utility.UIToolkit;

namespace DCL.Input.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(InputGroup))]
    public partial class UpdateShowHideUIInputSystem : BaseUnityLoopSystem
    {
        private readonly DCLInput dclInput;
        private readonly IMVCManager mvcManager;

        private SingleInstanceEntity camera;

        private bool currentUIVisibilityState = true;

        private UpdateShowHideUIInputSystem(World world, IMVCManager mvcManager) : base(world)
        {
            dclInput = DCLInput.Instance;
            this.mvcManager = mvcManager;
        }

        public override void Initialize()
        {
            camera = World.CacheCamera();
        }

        protected override void Update(float t)
        {
            // TODO: Should this really be in a system? It's not really updating anything, just checking for input triggers

            if (dclInput.Shortcuts.ShowHideUI.WasPressedThisFrame())
            {
                currentUIVisibilityState = !currentUIVisibilityState;

                // Common UIs
                mvcManager.SetAllViewsCanvasActive(currentUIVisibilityState);
            }
            else if (World.TryGet(camera, out ToggleUIRequest request))
            {
                currentUIVisibilityState = request.Enable;

                // Common UIs
                mvcManager.SetAllViewsCanvasActive(request.Except, currentUIVisibilityState);

                World.Remove<ToggleUIRequest>(camera);
            }

            foreach (UIDocument doc in UIDocumentTracker.ActiveDocuments)
                doc.rootVisualElement.SetDisplayed(currentUIVisibilityState);
        }
    }
}
