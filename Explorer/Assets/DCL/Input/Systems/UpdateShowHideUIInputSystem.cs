using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.Abstract;
using MVC;

namespace DCL.Input.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(InputGroup))]
    public partial class UpdateShowHideUIInputSystem : BaseUnityLoopSystem
    {
        private readonly DCLInput dclInput;
        private readonly MVCManager mvcManager;

        private bool nextUIVisibilityState;

        private UpdateShowHideUIInputSystem(World world, DCLInput dclInput, MVCManager mvcManager) : base(world)
        {
            this.dclInput = dclInput;
            this.mvcManager = mvcManager;
        }

        protected override void Update(float t)
        {
            if (dclInput.Shortcuts.ShowHideUI.WasPressedThisFrame())
            {
                mvcManager.SetAllViewsCanvasActive(nextUIVisibilityState);
                nextUIVisibilityState = !nextUIVisibilityState;
            }
        }
    }
}
