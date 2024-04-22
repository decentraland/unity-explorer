using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.DebugUtilities;
using ECS.Abstract;
using MVC;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.Input.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(InputGroup))]
    public partial class UpdateShowHideUIInputSystem : BaseUnityLoopSystem
    {
        private readonly DCLInput dclInput;
        private readonly MVCManager mvcManager;
        private readonly DebugContainerBuilder debugContainerBuilder;
        private readonly UIDocument rootUIDocument;
        private readonly UIDocument cursorUIDocument;

        private bool nextUIVisibilityState;

        private UpdateShowHideUIInputSystem(
            World world,
            DCLInput dclInput,
            MVCManager mvcManager,
            DebugContainerBuilder debugContainerBuilder,
            UIDocument rootUIDocument,
            UIDocument cursorUIDocument) : base(world)
        {
            this.dclInput = dclInput;
            this.mvcManager = mvcManager;
            this.debugContainerBuilder = debugContainerBuilder;
            this.rootUIDocument = rootUIDocument;
            this.cursorUIDocument = cursorUIDocument;
        }

        protected override void Update(float t)
        {
            if (!dclInput.Shortcuts.ShowHideUI.WasPressedThisFrame())
                return;

            // Common UIs
            mvcManager.SetAllViewsCanvasActive(nextUIVisibilityState);

            // Debug Panel UI
            debugContainerBuilder.IsVisible = nextUIVisibilityState;

            // Scenes UIs
            rootUIDocument.rootVisualElement.parent.style.display = nextUIVisibilityState ? DisplayStyle.Flex : DisplayStyle.None;

            // Cursor UI
            cursorUIDocument.rootVisualElement.parent.style.display = nextUIVisibilityState ? DisplayStyle.Flex : DisplayStyle.None;

            nextUIVisibilityState = !nextUIVisibilityState;
        }
    }
}
