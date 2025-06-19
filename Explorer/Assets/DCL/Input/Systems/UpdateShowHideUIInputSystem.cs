using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.CharacterCamera;
using DCL.DebugUtilities;
using DCL.InWorldCamera;
using ECS.Abstract;
using MVC;
using UnityEngine.UIElements;

namespace DCL.Input.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(InputGroup))]
    public partial class UpdateShowHideUIInputSystem : BaseUnityLoopSystem
    {
        private readonly DCLInput dclInput;
        private readonly IMVCManager mvcManager;
        private readonly IDebugContainerBuilder debugContainerBuilder;
        private readonly UIDocument rootUIDocument;
        private readonly UIDocument sceneUIDocument;
        private readonly UIDocument cursorUIDocument;

        private SingleInstanceEntity camera;

        private bool currentUIVisibilityState = true;

        private UpdateShowHideUIInputSystem(
            World world,
            IMVCManager mvcManager,
            IDebugContainerBuilder debugContainerBuilder,
            UIDocument rootUIDocument,
            UIDocument sceneUIDocument,
            UIDocument cursorUIDocument) : base(world)
        {
            dclInput = DCLInput.Instance;
            this.mvcManager = mvcManager;
            this.debugContainerBuilder = debugContainerBuilder;
            this.rootUIDocument = rootUIDocument;
            this.sceneUIDocument = sceneUIDocument;
            this.cursorUIDocument = cursorUIDocument;
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

            // Debug Panel UI
            debugContainerBuilder.Container.parent.style.display = currentUIVisibilityState ? DisplayStyle.Flex : DisplayStyle.None;

            // Root UIs (I think that's just cursor overlays?)
            rootUIDocument.rootVisualElement.parent.style.display = currentUIVisibilityState ? DisplayStyle.Flex : DisplayStyle.None;

            // Scenes UIs
            sceneUIDocument.rootVisualElement.parent.style.display = currentUIVisibilityState ? DisplayStyle.Flex : DisplayStyle.None;

            // Cursor UI
            cursorUIDocument.rootVisualElement.parent.style.display = currentUIVisibilityState ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
